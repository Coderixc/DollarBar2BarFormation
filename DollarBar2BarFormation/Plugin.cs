#define TRACE
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CustomResolutionsTypes;
using CustomResolutionsTypes.Controls;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using System.Data;
using System.Reflection;
using System.Collections.Generic;

namespace DollarBar2BarFormation
{
	[ComVisible(true)]
	[Guid("4ad950b3-d330-43aa-8adf-e9e97756d064")]
	[ClassInterface(ClassInterfaceType.None)]
	[CustomResolutionPluginAttribute(RuleOHLC=true)]
	public class Plugin : ICustomResolutionPlugin, ICustomPluginFormatParams, ICustomResolutionStyles, ICustomResolutionPluginSettings
	{
		#region Declare Variable
		private bool flag_TickblazeOutputwindow = false;

		private Indiactaor LibIndicator;


		private List<double> ListPrice_minutes = new List<double>();
		private List<double> ListVolume_minutes = new List<double>();

		private Queue<double> QueuePrice_mean;
		private Queue<double> QueueVolume_sum;
		private List<double> list_barsizevar = new List<double>();


		private double _barSize = 0;
		private double _barSizeVar = 0;

		private int PreviousDate = 0; //Past
		private int ProcessDate = 0; //Presnt
		private bool Flag_SingleUse = true;
		private double barSizeFix;

		#endregion


		#region Ctor
		public Plugin()
		{
			this.QueuePrice_mean = new Queue<double>();
			this.QueueVolume_sum = new Queue<double>();
			this.LibIndicator = new Indiactaor();

			//*** For now we have changed this to a default value of 5 Billion. If you Open BTCUSD with Bitfinex Data or XBTUSD - the data we send u in the email, you can see that bar is being sampled every time volume crosses 5,000,000,000
			this.barSizeFix = 50000;//5000000000;  //TODO : Load Default bar size while Loading Format Instruments..
		}
		#endregion

		#region ICustomResolutionPlugin
		public String Name
		{
			get
			{
				return "DollarBar2BarFormation";
			}
		}

		public String Guid
		{
			get
			{
				return "8eae21d0-9ce6-4d2b-8df5-0f4a939d907f";
			}
		}

		public String Description
		{
			get
			{
				return "Using Bar formation Pluggin";
			}
		}

		public String Vendor
		{
			get
			{
				return "BarFormation";
			}
		}

		#region Properties
		private int _quantity = DefaultSettings.Quantity;
        private int _current_Index_Bar = 0;
        private OHLC m_OHLC = new OHLC();

	





		private long m_Volume = 0;
		private long m_UpVolume = 0;
		private long m_DownVolume = 0;
		private double m_PointValue = 0.0001;
		private long m_MinMovement = 1;

		#endregion


		public void Init(IBaseOptions baseOptions, IParams customParams)
		{
			object obj = null;
			customParams.GetValue((int)EFields.QuantityField, out obj);
			if (obj != null)
			{
				_quantity = (int)obj;
			}

			Trace.TraceInformation(string.Format("Init {0}: Quantity={1}",
				ToString(), _quantity));
		}

		public void OnData(ICustomBar Bar, Int64 time_in_ticks, Int32 tickId, double open, double high, double low, double close, long volumeAdded, long upVolumeAdded, long downVolumeAdded, ECustomBarTrendType trend, bool isBarClose)
		{
			this.ListPrice_minutes.Add(close);
			this.ListVolume_minutes.Add(volumeAdded);
		



			//*** Have you checked the format of time_in_ticks ?
			//635726880600000000-- > 20150717000100
			//635726880600000000-- > 20150717000100
			//635726881200000000---> 20150717000200
			//635726881800000000---> 20150717000300

			string dt = DateTimeString(time_in_ticks);
			if (this.Flag_SingleUse)
			{
				this.PreviousDate = Convert.ToInt32(dt.Substring(0, 8));
				this.Flag_SingleUse = false;
			}




			this.ProcessDate = Convert.ToInt32(dt.Substring(0, 8));

			#region Resampling data for a Day in Multicharts
			if (this.PreviousDate != this.ProcessDate)
			{
				//Creating List1 which holds the closing prices.mean()
				double res = this.resample_WithMean(this.ListPrice_minutes);
				//this.ListPrice_mean.Add(res);
				this.QueuePrice_mean.Enqueue(res);

				//Creating List2 which holds the volumes.sum()
				double vol = this.resample_WithoutMean(this.ListVolume_minutes);
				//this.ListVolume_sum.Add(vol);
				this.QueueVolume_sum.Enqueue(vol);

				//this.minute_dollarvalue.Clear(); // Clear List when Resmapling is Done for a day
				this.ListPrice_minutes.Clear();
				this.ListVolume_minutes.Clear();

				this.PreviousDate = Convert.ToInt32(dt.Substring(0, 8));
			}
			#endregion

			int Threshold = Math.Max(this.QueuePrice_mean.Count, this.QueueVolume_sum.Count);
			if (Threshold >= 30)
			{
				this.list_barsizevar = this.LibIndicator.Multiply(this.QueuePrice_mean.ToList(), this.QueueVolume_sum.ToList());
				// Remove first elements
				this.QueuePrice_mean.Dequeue();
				this.QueueVolume_sum.Dequeue();
				this._barSizeVar = this.LibIndicator.Simple_MovingAverage(this.list_barsizevar, 30) / 50;
				this._barSize = _barSizeVar;
			}
			else
			{
				this._barSize = this.barSizeFix; //By Default : TODO Link with variable similar to Tick Blaze
			}

			m_Volume += volumeAdded;
			m_UpVolume += upVolumeAdded;
			m_DownVolume += downVolumeAdded;

			m_OHLC.Update(open, high, low, close, volumeAdded, upVolumeAdded, downVolumeAdded, time_in_ticks, tickId);
            Bar.UpdateBar(m_OHLC.Time_in_ticks, m_OHLC.TickId, m_OHLC.Open, m_OHLC.High, m_OHLC.Low, m_OHLC.Close, m_OHLC.BarVolume, m_OHLC.BarUpVolume, m_OHLC.BarDownVolume, m_OHLC.Trend, true, true);

            if (isBarClose)
			{
				if (m_Volume >= this._barSize)
				{
					m_Volume = 0;
					m_UpVolume = 0;
					m_DownVolume = 0;
					Bar.CloseBar();
					//_current_Index_Bar = 0;
					m_OHLC.Clear();
				}

			}

		}

		public void Reset()
		{
            _current_Index_Bar = 0;
            m_OHLC.Clear();

			this.QueueVolume_sum.Clear();
			this.QueuePrice_mean.Clear();
		}
		#endregion

		#region ICustomPluginFormatParams
		public void FormatParams(IParams customParams, IPriceScale priceScale, out string formattedParams)
        {
            formattedParams = Name;

			object quantity = null;
			customParams.GetValue((int)EFields.QuantityField, out quantity);

			string quantityText = quantity != null ? quantity.ToString() : DefaultSettings.Quantity.ToString();

			formattedParams = string.Format("{0} {1}", Name, quantityText);
        }
		#endregion

		#region ICustomResolutionStyles
		public Int32 StyleCount
		{
			get
			{
				return m_Styles.Length;
			}
		}
		public EStyleType GetStyle(Int32 Idx)
		{
			return m_Styles[Idx];
		}

		private EStyleType[] m_Styles = new EStyleType[] { EStyleType.OHLC, EStyleType.HLC, EStyleType.HL, EStyleType.Candlestick, EStyleType.HollowCandlestick, EStyleType.DotOnClose, EStyleType.LineOnClose };
		#endregion

		#region ICustomResolutionPluginSettings

		public void CreatePanel(IntPtr hWnd, out IntPtr hPanelWnd, IParams _params, IPriceScale priceScale)
		{
			try
			{
				if (m_panel == null)
				{
					m_panel = new PluginSettingsPanel(_params);
				}
				hPanelWnd = m_panel.Handle;
			}
			catch (System.Exception ex)
			{
				m_panel = null;
				hPanelWnd = IntPtr.Zero;
				Trace.TraceError(string.Format("CreatePanel {0}: {1}\r{2}", ToString(), ex.Message, ex.StackTrace));
			}
		}

		public bool ValidatePanel()
		{
			if (m_panel == null)
				return true;

			return m_panel.ValidateChildren();
		}

		private PluginSettingsPanel m_panel;
		#endregion


		#region Date EPOCH Time TO String
		private string DateTimeString(long epochtime)
		{
			string result = String.Empty;

			try
			{
				String dt = new DateTime(epochtime).ToString("yyyyMMddHHmmss");
				return dt;
			}
			catch (Exception ex)
			{

				return result;
			}



		}
		#endregion

		#region Calculating Mean
		private double mean(double totalsum, int noofsample)
		{
			if (flag_TickblazeOutputwindow)
			{
				MessageBox.Show("Entered into mean(Sum) Method");
			}
			double result = 0.0;
			if (noofsample <= 0)
				return -1.0;
			try
			{
				result = totalsum / noofsample;
				return result;

			}
			catch (Exception ex)
			{
				MessageBox.Show("Unhandled Exception in ExtractDateonly " + ex.ToString());
				result = 0.0;
				return result;
			}
		}
		#endregion

		#region Applying Resample
		private double resample_WithMean(List<double> List_Input, int freq = 1)
		{
			if (flag_TickblazeOutputwindow)
			{
				MessageBox.Show("Entered into Resampling  With Mean Method");
			}
			double sum = 0.0;
			double result = 0.0;
			int i = 0;
			try
			{
				for (i = 0; i < List_Input.Count; i++)
				{
					sum = sum + List_Input[i];

				}
				result = mean(sum, List_Input.Count);
				return result;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				return 0.0;
			}
		}
		#endregion

		#region Resampling Without Mean
		private double resample_WithoutMean(List<double> List_Input, int freq = 1)
		{
			if (flag_TickblazeOutputwindow)
			{
				MessageBox.Show("Entered into Resampling  Without Mean Method");
			}
			double sum = 0.0;
			double result = 0.0;
			int i = 0;
			try
			{
				for (i = 0; i < List_Input.Count; i++)
				{
					result = result + List_Input[i];

				}
				;
				return result;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				return 0.0;
			}
		}
		#endregion
	}


	#region  Indicator Function
	class Indiactaor
	{
		private List<double> LitsResult;

		public Indiactaor()
		{

			this.LitsResult = new List<double>();
		}


		#region Simple Moving average
		public double Simple_MovingAverage(List<double> listBar, int barback)
		{
			if (barback == 0)
				return 0; //Can't divide by 0

			double result = 0.0;
			int loopend = listBar.Count - barback;
			int idx = 0;
			string element = "";
			try
			{
				if (loopend < 0)
				{
					//return  TODO : Pending 
				}

				for (idx = listBar.Count - 1; idx > loopend - 1; idx--)
				{
					result += listBar[idx];
					element = element + listBar[idx] + ",";
				}
			}
			catch (Exception e)
			{
				MessageBox.Show(" Erorr(s) Ocuured in " + MethodInfo.GetCurrentMethod().Name + $"{e.Message }");

			}

			return (double)result / barback;
		}
		#endregion

		#region Multiply Two list
		public List<double> Multiply(List<double> list1, List<double> list2)
		{
			try
			{
				this.LitsResult.Clear();
				int Max = Math.Max(list1.Count, list2.Count);

				for (int i = 0; i < Max; i++)
				{
					try
					{

						double result = 0;

						if (list1.ElementAtOrDefault(i) == 0.0)
						{
							list1.Add(0.0);
						}
						if (list2.ElementAtOrDefault(i) == 0.0)
						{
							list2.Add(0.0);
						}

						result = list1.ElementAt(i) * list2.ElementAt(2);

						this.LitsResult.Add(result);


					}
					catch (Exception e)
					{

					}
				}

				return this.LitsResult;

			}
			catch (Exception ex)
			{
				string err = $"Error(s) Occured in {MethodInfo.GetCurrentMethod().Name} error message {ex.Message}  ";
				return this.LitsResult;
			}


		}

		#endregion
	}
	#endregion


	#region Panel

	#region Main
	public partial class PluginSettingsPanel : Form
	{
		private IParams m_params = null;
		mcErrorProvider m_mcErrorProvider;

		public PluginSettingsPanel(IParams _params)
		{
			InitializeComponent();

			EditQuantity.KeyPress += QuantityEdit_KeyPress;
			EditQuantity.CausesValidation = true;
			EditQuantity.Validating += new CancelEventHandler(QuantityEdit_Validating);

			m_mcErrorProvider = new mcErrorProvider();


			if (_params != null)
			{
				object val = null;
				_params.GetValue((int)EFields.QuantityField, out val);
				if (val != null)
				{
					EditQuantity.Text = val.ToString();
				}
				else
				{
                    EditQuantity.Text = DefaultSettings.Quantity.ToString();
                }

				m_params = _params;
			}

		}

        protected override void WndProc(ref Message m)
        {
            const int WMSetPluginBkColor = 0x0400 + 10;
            if (m.Msg == WMSetPluginBkColor)
            {
                int color = m.WParam.ToInt32();
                byte red = (byte)(m.WParam.ToInt32() & 0xFF);
                byte green = (byte)((m.WParam.ToInt32() & 0x00FF00) >> 8);
                byte blue = (byte)((m.WParam.ToInt32() & 0xff0000) >> 16);
                this.BackColor = Color.FromArgb(red, green, blue);
            }

            base.WndProc(ref m);
        }

		private void QuantityEdit_TextChanged(object sender, EventArgs e)
		{
            int quantity = 0;
            if (IsValidQuantity(EditQuantity.Text, out quantity))
			{
				if (m_params != null)
					m_params.SetValue((int)EFields.QuantityField, quantity);
			}
		}

		private void QuantityEdit_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
			{
				e.Handled = true;
			}
			
		}

		private void QuantityEdit_Validating(object sender, CancelEventArgs e)
		{
            bool isValid = IsValidQuantity(EditQuantity.Text);

			if (!isValid)
				m_mcErrorProvider.SetError(EditQuantity, "Please choose a value between 1 and " + int.MaxValue.ToString());
			else
				m_mcErrorProvider.SetError(EditQuantity, "");

			e.Cancel = !isValid;
		}

        private bool IsValidQuantity(string textQuantity)
        {
            int quantity = 0;
            return IsValidQuantity(textQuantity, out quantity);
        }
        private bool IsValidQuantity(string textQuantity, out int value)
        {
            int quantity = 0;
            bool isValid = !string.IsNullOrEmpty(textQuantity) && int.TryParse(textQuantity, out quantity) && quantity > 0;
            value = isValid ? quantity : DefaultSettings.Quantity;
            return isValid;
        }

    }
	#endregion

	#region Designer
	partial class PluginSettingsPanel
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.LabelQuantity = new System.Windows.Forms.Label();
            this.EditQuantity = new System.Windows.Forms.TextBox();

			this.NumberOfdays = new System.Windows.Forms.TextBox();

			this.SuspendLayout();
            // 
            // LabelQuantity
            // 
            this.LabelQuantity.AutoSize = true;
            this.LabelQuantity.Location = new System.Drawing.Point(16, 5);
            this.LabelQuantity.Name = "LabelQuantity";
            this.LabelQuantity.Size = new System.Drawing.Size(51, 13);
            this.LabelQuantity.TabIndex = 7;
            this.LabelQuantity.Text = "Quantity:";
            this.LabelQuantity.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // EditQuantity
            // 
            this.EditQuantity.Location = new System.Drawing.Point(117, 4);
            this.EditQuantity.AutoSize = false;
            this.EditQuantity.Name = "EditQuantity";
            this.EditQuantity.Size = new System.Drawing.Size(60, 21);
            this.EditQuantity.TabIndex = 1;
            this.EditQuantity.TextChanged += new System.EventHandler(this.QuantityEdit_TextChanged);
            // 
            // PluginSettingsPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(380, 102);
            this.Controls.Add(this.EditQuantity);
            this.Controls.Add(this.LabelQuantity);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "PluginSettingsPanel";
            this.Text = "PluginSettingsPanel";
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label LabelQuantity;
        private System.Windows.Forms.TextBox EditQuantity;
		private System.Windows.Forms.TextBox NumberOfdays;
	}
    #endregion

    #endregion

    #region Helper
    class OHLC
    {
        public double Open { get; private set; }
        public double High { get; private set; }
        public double Low { get; private set; }
        public double Close { get; private set; }
        public long BarVolume { get; private set; }
        public long BarUpVolume { get; private set; }
        public long BarDownVolume { get; private set; }
        public long Time_in_ticks { get; private set; }
        public int TickId { get; private set; }
        public bool Init { get; private set; }
        public ECustomBarTrendType Trend { get { return Close >= Open ? ECustomBarTrendType.TrendUp : ECustomBarTrendType.TrendDown; } }

        public OHLC Copy()
        {
            return new OHLC()
            {
                Open = Open,
                High = High,
                Low = Low,
                Close = Close,
                BarVolume = BarVolume,
                BarUpVolume = BarUpVolume,
                BarDownVolume = BarDownVolume,
                Time_in_ticks = Time_in_ticks,
                TickId = TickId,
                Init = Init
            };
        }

        public OHLC()
        {
            Clear();
        }

        public void Update(double open, double high, double low, double close, long barVolume, long barUpVolume, long barDownVolume, long time_in_ticks, int tickId)
        {
            if (!Init)
            {
                Init = true;
                Open = open;
                High = high;
                Low = low;
                Close = close;
            }
            else
            {
                if (High < high)
                {
                    High = high;
                }
                if (Low > low)
                {
                    Low = low;
                }
                Close = close;
            }
            BarVolume += barVolume;
            BarUpVolume += barUpVolume;
            BarDownVolume += barDownVolume;
            Time_in_ticks = time_in_ticks;
            TickId = tickId;
        }

        public void Clear()
        {
            Init = false;
            Open = 0;
            High = 0;
            Low = 0;
            Close = 0;
            BarVolume = 0;
            BarUpVolume = 0;
            BarDownVolume = 0;
            Time_in_ticks = 0;
            TickId = 0;
        }
    }

    public enum EFields
	{
		QuantityField = 0
	}

	static class DefaultSettings
	{
		static public int Quantity { get { return 1; } }
	}

	#endregion
}

