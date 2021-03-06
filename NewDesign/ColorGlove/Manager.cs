﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;

using System.Diagnostics;
using System.Drawing.Imaging;
using Microsoft.Kinect;

namespace ColorGlove
{

    public class Manager
    {
        private KinectData data;
        private Processor processor;
        public enum ProcessorModeFormat {
            Arun,
            Michael,
        }
        Thread poller;
        DataFeed datafeed;
        private ProcessorModeFormat processor_mode_ = ProcessorModeFormat.Michael; // set the mode for processor here

        public Manager(MainWindow parent)  // Construct function
        {
            if (processor_mode_ == ProcessorModeFormat.Michael)
                datafeed = new DataFeed(DataFeed.DataSource.Kinect, DataFeed.RangeModeFormat.Near);
            else
                datafeed = new DataFeed(DataFeed.DataSource.Kinect, DataFeed.RangeModeFormat.Default);
            
            #region Create one processor
            // the processor is passed by the current object and the main window controller
            // Note about the new design: there is only one processor which do all the processing, and pass the processed image to a
            // corresponding image control element
            processor = new Processor(processor_mode_, parent);
            processor.lower = 100;
            processor.upper = 1000;
            setDensity(0.91);
            setRadius(10);
            //    Image image = processors[i].getImage();
            //    parent.mainContainer.Children.Add(image);
            #endregion

            #region Processor configurations
            if (processor_mode_ == ProcessorModeFormat.Michael)
            {
                // Whic pipelines to show is decided by the processor.
                processor.setFeatureExtraction(Processor.ShowExtractedFeatureFormat.PredictAllPixelsGPU); 
                
                /*
                processors[0].updatePipeline(
                    // Show the rgb image
                                            Filter.Step.CopyColor
                    // Show the depth image                                         
                    // Filter.Step.Depth    

                    // Show the Color Labelling                                         
                    // Filter.Step.PaintWhite,
                    // Filter.Step.MatchColors
                    );

                //processors[0].updatePipeline(Filter.Step.ColorMatch);
                //processors[1].SetTestModule(Processor.ShowExtractedFeatureFormat.PredictOnePixelCPU | Processor.ShowExtractedFeatureFormat.ShowTransformedForOnePixel); // 
                // one should call SetTestModule to active the FeatureExtractionLib
                processors[1].setFeatureExtraction(Processor.ShowExtractedFeatureFormat.PredictAllPixelsGPU); 
                processors[1].updatePipeline(
                    // Show the rgb image
                    // Filter.Step.CopyColor
                    // Show the depth image                                         
                                            Filter.Step.CopyDepth,
                                            Filter.Step.EnablePredict,
                                            Filter.Step.PerPixelClassificationOnEnable,
                                            Filter.Step.PoolingOnPerPixelClassification,
                                            Filter.Step.ShowOverlay
                    // Show Mapped Depth Using RGB
                    // Filter.Step.PaintWhite,
                    // Filter.Step.MappedDepth
                    // Show the Color Labelling
                    // Filter.Step.PaintWhite,
                    // Filter.Step.MatchColors
                    // Denoise
                    //                        Filter.Step.Denoise
                );
                 */ 
            }
            else if (processor_mode_ == ProcessorModeFormat.Arun) {
                /*
                processors[0].setFeatureExtraction(Processor.ShowExtractedFeatureFormat.ShowTransformedForOnePixel); 
                processors[0].updatePipeline(
                    Filter.Step.PaintWhite,
                    Filter.Step.Crop,
                    Filter.Step.PaintGreen,
                    //Filter.Step.CopyColor,
                    //Filter.Step.MatchColors,
                    //Filter.Step.FeatureExtractOnEnable,
                    Filter.Step.ShowOverlay
                );


                processors[1].setFeatureExtraction(Processor.ShowExtractedFeatureFormat.PredictAllPixelsGPU);
                processors[1].updatePipeline(
                    //Filter.Step.PaintGreen,
                    //Filter.Step.Crop,
                    Filter.Step.CopyDepth,
                    Filter.Step.EnablePredict,
                    Filter.Step.PerPixelClassificationOnEnable,
                    Filter.Step.PoolingOnPerPixelClassification,
                    Filter.Step.ShowOverlay);
                
                */ 
                /*processors[1].SetTestModule(Processor.ShowExtractedFeatureFormat.ShowTransformedForOnePixel);
                processors[1].updatePipeline(
                   Processor.Step.PaintGreen,
                   Processor.Step.Depth,
                   Processor.Step.OverlayOffset
                );*/
                
            }
            #endregion

            // why using a new thread? (Michael)
            poller = new Thread(new ThreadStart(this.poll));
        }

        public void setRadius(int radius)
        {
            processor.radius_ = radius;
        }

        public void setDensity(double density)
        {
            processor.density_ = density;
        }

        public void start()
        {
            poller.Start();
        }

        public void toggleProcessors()
        {
            if (poller.ThreadState == System.Threading.ThreadState.Suspended) poller.Resume(); 
            else poller.Suspend();
        }


        public void saveImages()
        {
            processor.EnableFeatureExtract();  
            //foreach(Processor p in processors) p.EnableFeatureExtract();
            //poller.Suspend();
            //foreach (Processor p in processors) p.processAndSave();
            //processors[0].ProcessAndSave();
            // use processor[0] to save the depth image
            //poller.Resume();
        }


        public void AutoRange()
        {
           /*
            processors[0].AutoDetectRange();
            processors[1].AutoDetectRange();
            */
            processor.AutoDetectRange();
        }

        public void increaseRange()
        {
            /*
            processors[0].IncreaseRange();
            processors[1].IncreaseRange();
            */
            processor.IncreaseRange();
        }

        public void decreaseRange()
        {
            /*
            processors[0].DecreaseRange();
            processors[1].DecreaseRange();
            */
            processor.DecreaseRange();
        }

        public void poll()
        {
            while (true)
            {
                data = datafeed.PullData(); 
                /*
                foreach (Processor p in processors) p.update(data);
                */
                processor.update(data);
            }
        }


        public void kMeans() { 
            //processors[0].kMeans(); 
            processor.kMeans();
        }
        

        // Just adds pooling to the pipeline. 
        // XXX: This doesn't have to happen for only processor 1. Pooling can 
        // be enabled for all processors and only the ones with the pooling 
        // added to the pipeline will then perform that action.
        public void Pool() 
        {
            //processors[1].EnablePredict(); 
            processor.EnablePredict();
        }
    }
}
