using NAudio.Wave;

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AudioVisualizer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private IWaveIn waveIn;
        private static readonly int fftLength = 1024; // Has to be powers of two!
        private readonly SampleAggregator sampleAggregator = new SampleAggregator(fftLength);

        private void MainForm_Load(object sender, EventArgs e)
        {
            //Add fft event handler
            sampleAggregator.FftCalculated += new EventHandler<FftEventArgs>(FftCalculated);
            sampleAggregator.PerformFFT = true;

            //Setting up the chart1
            chart1.ChartAreas[0].AxisY.Minimum = 0;
            chart1.ChartAreas[0].AxisY.Maximum = 0.01;
            chart1.ChartAreas[0].AxisX.LineWidth = 0;
            chart1.ChartAreas[0].AxisY.LineWidth = 0;
            chart1.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
            chart1.ChartAreas[0].AxisY.LabelStyle.Enabled = false;
            chart1.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
            chart1.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
            chart1.ChartAreas[0].AxisX.MinorGrid.Enabled = false;
            chart1.ChartAreas[0].AxisY.MinorGrid.Enabled = false;
            chart1.ChartAreas[0].AxisX.MajorTickMark.Enabled = false;
            chart1.ChartAreas[0].AxisY.MajorTickMark.Enabled = false;
            chart1.ChartAreas[0].AxisX.MinorTickMark.Enabled = false;
            chart1.ChartAreas[0].AxisY.MinorTickMark.Enabled = false;
            chart1.Series[0].IsVisibleInLegend = false;
            chart1.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Column;
            chart1.Series[0]["PointWidth"] = "1";

            //Set waveIn to WasapiLoopbackCapture to capture the system Audio
            waveIn = new WasapiLoopbackCapture();

            waveIn.DataAvailable += OnDataAvailable;

            waveIn.StartRecording();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<WaveInEventArgs>(OnDataAvailable), sender, e);
            }
            else
            {
                byte[] buffer = e.Buffer;
                int bytesRecorded = e.BytesRecorded;
                int bufferIncrement = waveIn.WaveFormat.BlockAlign;

                for (int index = 0; index < bytesRecorded; index += bufferIncrement)
                {
                    float sample32 = BitConverter.ToSingle(buffer, index);
                    sampleAggregator.Add(sample32);
                }
            }
        }

        private List<double> lastFft = new List<double>();

        private void FftCalculated(object sender, FftEventArgs e)
        {
            List<double> fft = new List<double>();

            //Adding values form e.Result to fft List
            for (int i = 0; i < e.Result.Length / 2 - 70; i++)
            {
                fft.Add(Math.Abs(e.Result[i].Y * i / (200 - volumeTrackBar.Value)));
            }

            //set volumeLabel to the current volume
            volumeLabel.Text = "Volume: " + (volumeTrackBar.Value + 1) + " | " + (200 - volumeTrackBar.Value);

            //Check if the previous fft list isn't 0
            if (lastFft.Count != 0)
            {
                //Dampening the fft
                for (int i = 0; i < fft.Count; i++)
                {
                    if (fft[i] > lastFft[i] || bufferValueNumericUpDown.Value == 0)
                    {
                        fft[i] = (fft[i] + lastFft[i]) / 2;
                    }
                    else
                    {
                        fft[i] = lastFft[i] - (double)bufferValueNumericUpDown.Value / 1000;
                    }
                }
            }

            int barCount = (int)barCountNumericUpDown.Value;
            List<double> scaledFft = new List<double>();

            //Calculate volume of bars
            if (barCount > 0)
            {
                int count = fft.Count / barCount;
                for (int i = 0; i < barCount; i++)
                {
                    double temp = 0;
                    int j;
                    for (j = i * count; j < count + i * count; j++)
                    {
                        temp += fft[j];
                    }

                    scaledFft.Add(temp);
                }
            }
            else
            {
                scaledFft.AddRange(fft);
            }

            #region flatten

            int flattenValue = (int)flattenValueNumericUpDown.Value;

            //flatten the values
            for (int i = 0; i < scaledFft.Count; i++)
            {
                if (flattenValue > 0)
                {
                    double temp = 0;
                    for (int j = 0; j < flattenValue; j++)
                    {
                        if (i + j < scaledFft.Count)
                        {
                            temp += scaledFft[i + j];
                        }
                    }
                    scaledFft[i] = temp / flattenValue;
                }
            }

            for (int i = 0; i < scaledFft.Count; i++)
            {
                if (flattenValue > 0)
                {
                    double temp = 0;
                    for (int j = 0; j < flattenValue; j++)
                    {
                        if (i - j >= 0)
                        {
                            temp += scaledFft[i - j];
                        }
                    }
                    scaledFft[i] = temp / flattenValue;
                }
            }

            #endregion flatten

            //Clear and add current data to chart1
            lastFft = fft;
            chart1.Series[0].Points.Clear();

            for (int i = 0; i < scaledFft.Count; i++)
            {
                chart1.Series[0].Points.AddY(scaledFft[i]);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
    }
}