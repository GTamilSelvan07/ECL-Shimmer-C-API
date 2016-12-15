﻿using ShimmerAPI;
using ShimmerLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShimmerConsoleAppExample
{
    class Program
    {
        Filter LPF_PPG;
        Filter HPF_PPG;
        private PPGtoHRAlgorithm PPGtoHeartRateCalculation;
        private int NumberOfHeartBeatsToAverage = 1;
        private int TrainingPeriodPPG = 10; //10 second buffer
        double LPF_CORNER_FREQ_HZ = 5;
        double HPF_CORNER_FREQ_HZ = 0.5;
        Shimmer Shimmer;
        double SamplingRate = 128;
        int Count = 0;
        public bool FirstTime = true;

        //The index of the signals originating from ShimmerBluetooth 
        int IndexAccelX;
        int IndexAccelY;
        int IndexAccelZ;
        int IndexGSR;
        int IndexPPG;
        int IndexTimeStamp;

        static void Main(string[] args)
        {
            System.Console.WriteLine("Hello");
            Program p = new Program();
            p.start();
        }

        public void start()
        {
            //Setup PPG to HR filters and algorithm
            PPGtoHeartRateCalculation = new PPGtoHRAlgorithm(SamplingRate, NumberOfHeartBeatsToAverage, TrainingPeriodPPG);
            LPF_PPG = new Filter(Filter.LOW_PASS, SamplingRate, new double[] { LPF_CORNER_FREQ_HZ });
            HPF_PPG = new Filter(Filter.HIGH_PASS, SamplingRate, new double[] { HPF_CORNER_FREQ_HZ });


            int enabledSensors = ((int)Shimmer.SensorBitmapShimmer3.SENSOR_A_ACCEL| (int)Shimmer.SensorBitmapShimmer3.SENSOR_GSR| (int)Shimmer.SensorBitmapShimmer3.SENSOR_INT_A13);
            //int enabledSensors = ((int)Shimmer.SensorBitmapShimmer3.SENSOR_A_ACCEL | (int)Shimmer.SensorBitmapShimmer3.SENSOR_EXG1_24BIT | (int)Shimmer.SensorBitmapShimmer3.SENSOR_EXG2_24BIT); 

            //shimmer = new Shimmer("ShimmerID1", "COM17");
            Shimmer = new Shimmer("ShimmerID1", "COM18", SamplingRate, 0, ShimmerBluetooth.GSR_RANGE_AUTO, enabledSensors, false, false, false, 1, 0, Shimmer3Configuration.EXG_EMG_CONFIGURATION_CHIP1, Shimmer3Configuration.EXG_EMG_CONFIGURATION_CHIP2, true);

            Shimmer.UICallback += this.HandleEvent;
            Shimmer.Connect();

        }
        public void HandleEvent(object sender, EventArgs args)
        {
            CustomEventArgs eventArgs = (CustomEventArgs)args;
            int indicator = eventArgs.getIndicator();

            switch (indicator)
            {
                case (int)Shimmer.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE:
                    System.Diagnostics.Debug.Write(((Shimmer)sender).GetDeviceName() + " State = " + ((Shimmer)sender).GetStateString() + System.Environment.NewLine);
                    int state = (int)eventArgs.getObject();
                    if (state == (int)Shimmer.SHIMMER_STATE_CONNECTED)
                    {
                        Shimmer.StartStreaming();
                        System.Console.WriteLine("Shimmer is Connected");
                    }
                    else if (state == (int)Shimmer.SHIMMER_STATE_CONNECTING)
                    {
                        System.Console.WriteLine("Establishing Connection to Shimmer Device");
                    }
                    else if (state == (int)Shimmer.SHIMMER_STATE_NONE)
                    {

                    }
                    else if (state == (int)Shimmer.SHIMMER_STATE_STREAMING)
                    {
                        System.Console.WriteLine("Shimmer is Streaming");
                    }
                    break;
                case (int)Shimmer.ShimmerIdentifier.MSG_IDENTIFIER_NOTIFICATION_MESSAGE:
                    break;
                case (int)Shimmer.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET:
                    ObjectCluster objectCluster = (ObjectCluster)eventArgs.getObject();
                    if (FirstTime)
                    {
                        IndexAccelX = objectCluster.GetIndex("Low Noise Accelerometer X", "CAL");
                        IndexAccelY = objectCluster.GetIndex("Low Noise Accelerometer Y", "CAL");
                        IndexAccelZ = objectCluster.GetIndex("Low Noise Accelerometer Z", "CAL");
                        IndexGSR = objectCluster.GetIndex("GSR", "CAL");
                        IndexPPG = objectCluster.GetIndex("Internal ADC A13", "CAL");
                        IndexTimeStamp = objectCluster.GetIndex("Timestamp", "CAL");
                        FirstTime = false;
                    }
                    SensorData datax = objectCluster.GetData(IndexAccelX);
                    SensorData datay = objectCluster.GetData(IndexAccelY);
                    SensorData dataz = objectCluster.GetData(IndexAccelZ);
                    SensorData dataGSR = objectCluster.GetData(IndexGSR);
                    SensorData dataPPG = objectCluster.GetData(IndexPPG);
                    SensorData dataTS = objectCluster.GetData(IndexTimeStamp);

                    //Process PPG signal and calculate heart rate
                    double dataFilteredLP = LPF_PPG.filterData(dataPPG.Data);
                    double dataFilteredHP = HPF_PPG.filterData(dataFilteredLP);
                    int heartRate = (int)Math.Round(PPGtoHeartRateCalculation.ppgToHrConversion(dataFilteredHP, dataTS.Data));


                    if (Count % SamplingRate == 0) //only display data every second
                    {
                        System.Console.WriteLine("AccelX: " + datax.Data + " AccelY: " + datay.Data + " AccelZ: " + dataz.Data);
                        System.Console.WriteLine("GSR: " + dataGSR.Data + " PPG: " + dataPPG.Data + " HR: " + heartRate);
                    }
                    Count++;
                    break;
            }
        }
    }
}