﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cloo;

namespace FeatureExtractionLib
{
    public class GPUCompute
    {
        private ComputeProgram program_;
        #region predict_release
        private string clProgramSource_predict_ = @"
short GetNewDepth(int cur_index, int dx, int dy, global read_only short* depth)
{    
    float tmp = 1/ (float)depth[cur_index];
    int cx = (int) ( (cur_index % 640) +  (float)dx * tmp  );
    int cy = (int) ( (cur_index / 640) + (float)dy * tmp );
    if (cx>=0 && cx< 640 && cy>=0 && cy< 480)
    {
        if (depth[cy*640 + cx] <0)
            return 10000;
        else
            return depth[cy*640 + cx];
    }
    else 
        return 10000;  
} 
kernel void Predict(
    global read_only short* meta_tree,     
    global read_only int* trees, 
    global read_only int* offset_list,
    global write_only float* y,    
    global read_only short* depth)
{
    
    int index= get_global_id(0), y_index =index* meta_tree[0];    
    int offs = 0, k,  offset_list_index;    
    short u_depth, v_depth, i;    
    float v;    
    if (depth[index] == -1)
    {
        y[y_index] = 1;
        for (i=1; i< meta_tree[0]; i++)
            y[y_index + i] = 0;
        return;
    }
    v = (float)1 / (float)meta_tree[1];
    for (i=0; i< meta_tree[0]; i++)
        y[y_index + i] = 0;
    for (i=0; i< meta_tree[1]; i++){
        k = offs +1;
        while (1){
            if (trees[k] == -1)
            {                
                y[y_index + trees[k+1]]++;
                break;
            }
            // get the feature value
            offset_list_index = trees[k]*4;                        
            u_depth = GetNewDepth(index, offset_list[offset_list_index], offset_list[offset_list_index+1], depth);
            v_depth = GetNewDepth(index, offset_list[offset_list_index + 2], offset_list[offset_list_index+3], depth);            
            if (u_depth - v_depth < trees[k+1] )
                k+=3;
            else
                k = offs + trees[k+2];
        }
        offs = offs + trees[offs];
    }
    for (i=0; i< meta_tree[0]; i++)
        y[y_index + i] = v* y[y_index + i];
    
}
";
        #endregion

        /*
        breadth first
         */
        #region breadth first
        private string clProgramSource_predict_test_breath_first_tree_ = @"
short GetNewDepth(int cur_x, int cur_y, int cur_index, int dx, int dy, global read_only short* depth)
{    
    /* 
    int cx = (int) ( (float)cur_x +  (float)dx / (float)depth[cur_index] );
    int cy = (int) ( (float)cur_y + (float)dy / (float)depth[cur_index] );         
    */
    float v= 1 / (float)depth[cur_index] ;
    int cx = (int) ( (float)cur_x +  (float)dx * v  );
    int cy = (int) ( (float)cur_y + (float)dy * v );         
    //return depth[cy*640 + cx];    
    if (cx>=0 && cx< 640 && cy>=0 && cy< 480)
    {
        
        if (depth[cy*640 + cx] <0)
            return 10000;
        else
            return depth[cy*640 + cx];        
    }
    else 
        return 10000;  
   
} 
kernel void Predict(
    global read_only short* meta_tree,     
    global read_only int* trees, // 12 MB
    global read_only const int* offset_list, // 32 KB
    global write_only float* y, // 3.6MB   
    global read_only short* depth // 1.2 MB
)
{
    
    int index= get_global_id(0), y_index =index* meta_tree[0];    
    int offs = 0, k, offset_list_index, visit_count = 0, new_k;    
    short u_depth, v_depth, i;    
    int cur_x= index % 640, cur_y=index / 640;
    float v;        
    if (depth[index] == -1)
    {
        y[y_index] = 1;
        for (i=1; i< meta_tree[0]; i++)
            y[y_index + i] = 0;
        return;
    }

    v = (float)1 / (float)meta_tree[1];
    for (i=0; i< meta_tree[0]; i++)
        y[y_index + i] = 0;
    // do some repeatable stuff
    //for (i=0; i< 60; i++);

    for (i=0; i< meta_tree[1]; i++){
        k = offs +1;     
        visit_count = 0;   
        while (1){            
            visit_count ++;
             
            // limit the depth      
            if (visit_count>20)
                break;
            
            if (trees[k] == -1)
            {                
                y[y_index + trees[k+1]]++;
                break;
            }
            // get the feature value
            offset_list_index = trees[k]*4;            
            
            //u_depth = 0;
            //v_depth = 0;
            u_depth = GetNewDepth(cur_x, cur_y, index, offset_list[offset_list_index], offset_list[offset_list_index+1], depth);
            v_depth = GetNewDepth(cur_x, cur_y, index, offset_list[offset_list_index + 2], offset_list[offset_list_index+3], depth);                  
            // use Alglib tree
            /* 
            if (u_depth - v_depth < trees[k+1] )
                k+=3;
            else
                k = offs + trees[k+2];
            */
            // use breadth-first tree
            // first go to the left child by default
            new_k = trees[k+2] + offs;
            if (u_depth - v_depth >= trees[k+1] )
            {                
                // if the left child is not a leaf
                if (trees[new_k] != -1)
                    new_k+=3;
                else
                    new_k+=2;                
            }    
            k = new_k;
            // just traverse 
            /*
            k = offs + trees[k];
            */
        }
        offs = offs + trees[offs];
    }

    for (i=0; i< meta_tree[0]; i++)
        y[y_index + i] = v* y[y_index + i];
        //y[y_index + i] = visit_count;
    
}
";
        #endregion
        /*
        2d test
         */
        #region test2D
        private string clProgramSource_predict_test_2d_ = @"
short GetNewDepth(int cur_x, int cur_y, int cur_index, int dx, int dy, global read_only short* depth)
{        
    int cx = (int) ( cur_x +  (float)dx / (float)depth[cur_index] );
    int cy = (int) ( cur_y + (float)dy / (float)depth[cur_index] );
    if (cx>=0 && cx< 640 && cy>=0 && cy< 480)
    {
        if (depth[cy*640 + cx] <0)
            return 10000;
        else
            return depth[cy*640 + cx];
    }
    else 
        return 10000;  
} 
kernel void Predict(
    global read_only short* meta_tree,     
    global read_only int* trees, 
    global read_only int* offset_list,
    global write_only float* y,    
    global read_only short* depth)
{
    
    int cx= get_global_id(0), cy=get_global_id(1);
    int index=cy*640 + cx, y_index =index* meta_tree[0];    
    int offs = 0, k, offset_list_index, visit_count = 0;    
    short u_depth, v_depth, i;    
    float v;        
    
    if (depth[index] == -1)
    {
        y[y_index] = 1;
        for (i=1; i< meta_tree[0]; i++)
            y[y_index + i] = 0;
        return;
    }

    v = (float)1 / (float)meta_tree[1];
    for (i=0; i< meta_tree[0]; i++)
        y[y_index + i] = 0;    

    for (i=0; i< meta_tree[1]; i++){
        k = offs +1;     
        visit_count = 0;   
        while (1){            
            visit_count ++;
            // limit the depth
            /*
            if (visit_count>20)
                break;
            */
            if (trees[k] == -1)
            {                
                y[y_index + trees[k+1]]++;
                break;
            }
            // get the feature value
            offset_list_index = trees[k]*4;            
            //u_depth = 0;
            //v_depth = 0;
            u_depth = GetNewDepth(cx, cy, index, offset_list[offset_list_index], offset_list[offset_list_index+1], depth);
            v_depth = GetNewDepth(cx, cy, index, offset_list[offset_list_index + 2], offset_list[offset_list_index+3], depth);                  
            
            if (u_depth - v_depth < trees[k+1] )
                k+=3;
            else
                k = offs + trees[k+2];
            
        }
        offs = offs + trees[offs];
    }


    for (i=0; i< meta_tree[0]; i++)
        //y[y_index + i] *= v;
        y[y_index + i] = visit_count;
    
}
";
        #endregion
        /*
       2d test
        */
        #region release2D
        private string clProgramSource_predict_2d_ = @"
short GetNewDepth(int cur_x, int cur_y, int cur_index, int dx, int dy, global read_only short* depth)
{        
    int cx = (int) ( cur_x +  (float)dx / (float)depth[cur_index] );
    int cy = (int) ( cur_y + (float)dy / (float)depth[cur_index] );
    if (cx>=0 && cx< 640 && cy>=0 && cy< 480)
    {
        if (depth[cy*640 + cx] <0)
            return 10000;
        else
            return depth[cy*640 + cx];
    }
    else 
        return 10000;  
} 
kernel void Predict(
    global read_only short* meta_tree,     
    global read_only int* trees, 
    global read_only int* offset_list,
    global write_only float* y,    
    global read_only short* depth)
{
    
    int cx= get_global_id(0), cy=get_global_id(1);
    int index=cy*640 + cx, y_index =index* meta_tree[0];    
    int offs = 0, k, offset_list_index, visit_count = 0;    
    short u_depth, v_depth, i;    
    float v;        
    
    if (depth[index] == -1)
    {
        y[y_index] = 1;
        for (i=1; i< meta_tree[0]; i++)
            y[y_index + i] = 0;
        return;
    }

    v = (float)1 / (float)meta_tree[1];
    for (i=0; i< meta_tree[0]; i++)
        y[y_index + i] = 0;    

    for (i=0; i< meta_tree[1]; i++){
        k = offs +1;     
        visit_count = 0;   
        while (1){            
            visit_count ++;
            // limit the depth
            /*
            if (visit_count>20)
                break;
            */
            if (trees[k] == -1)
            {                
                y[y_index + trees[k+1]]++;
                break;
            }
            // get the feature value
            offset_list_index = trees[k]*4;            
            //u_depth = 0;
            //v_depth = 0;
            u_depth = GetNewDepth(cx, cy, index, offset_list[offset_list_index], offset_list[offset_list_index+1], depth);
            v_depth = GetNewDepth(cx, cy, index, offset_list[offset_list_index + 2], offset_list[offset_list_index+3], depth);                  
            
            if (u_depth - v_depth < trees[k+1] )
                k+=3;
            else
                k = offs + trees[k+2];            
        }
        offs = offs + trees[offs];
    }

    for (i=0; i< meta_tree[0]; i++)
        y[y_index + i] *= v;            
}
";
        #endregion

        #region random forest process
        private string clProgramSource_dfprocess_ = @"
kernel void DFProcess(
    global read_only short* meta_tree, 
    global read_only int* trees, 
    global read_only short* x,
    global write_only float* y)
{
    int index= get_global_id(0);    
    int offs = 0, k, idx;
    short i;
    float v;
    v = (float)1 / (float)meta_tree[1];
    for (i=0; i< meta_tree[0]; i++)
        y[i] = 0;
    for (i=0; i< meta_tree[1]; i++){
        k = offs +1;
        while (1){
            if (trees[k] == -1)
            {
                idx = trees[k+1];
                y[idx]++;
                break;
            }
            if (x[trees[k]] < trees[k+1] )
                k+=3;
            else
                k = offs + trees[k+2];
        }
        offs = offs + trees[offs];
    }
    for (i=0; i< meta_tree[0]; i++)
        y[i] = v* y[i];
}  
";
        #endregion

        #region vector add
        private string clProgramSource_vector_add_ = @"
short AddVector(short a, global read_only int* t, int index)
{
    return a + t[index];
}
/*
short AddVector(short a, short b, global read_only int* t)
{
    return a + b;
}
*/
kernel void AddVectorWithTrees(
    global  read_only short* a, 
    global  read_only int* trees,     
    global  write_only short* c)
{
    int index = get_global_id(0);    
//    c[index] = a[index] + (short) (trees[index]);
//    c[index] =AddVector(a[index], (short)(trees[index]), trees);
    c[index] =AddVector(a[index], trees, index);
}
";
        #endregion
        private ComputeKernel kernel_;
        private ComputeContext context_;
        private ComputeCommandQueue commands_;

        private ComputeBuffer<short> a_;
        private ComputeBuffer<short> c_;
        private ComputeBuffer<int> trees_;
        private ComputeBuffer<short> meta_tree_;
        // list of offsets (ux, uy, vx, vy)
        private ComputeBuffer<int> offset_list_;
        // feature vector
        private ComputeBuffer<short> x_;
        // depth
        private ComputeBuffer<short> depth_;
        // predict output
        private ComputeBuffer<float> y_; 
        private int count_;
        private ComputeModeFormat compute_mode_;

        // enum
        public enum ComputeModeFormat { 
            kAddVectorTest = 1,
            kPredictWithFeaturesTest = 2,
            kRelease = 4,
            kTestBreathFrist = 8,
            kTest2D = 16,
            kRelease2D = 32,
        };
        
        // Constructor function
        public GPUCompute(ComputeModeFormat SetComputeMode = ComputeModeFormat.kRelease)         
        {
            ComputePlatform platform = ComputePlatform.Platforms[0];
            ComputeContextPropertyList properties = new ComputeContextPropertyList(platform);
            IList<ComputeDevice> devices = new List<ComputeDevice>();
            devices.Add(platform.Devices[0]);
            Console.WriteLine("Platform name: {0}", platform.Devices[0].Name);
            context_ = new ComputeContext(devices, properties, null, IntPtr.Zero);
            compute_mode_ = SetComputeMode;
            Console.WriteLine("Compute Mode: {0}", compute_mode_);
            // built the GPU program            
            if (compute_mode_ == ComputeModeFormat.kAddVectorTest)
                program_ = new ComputeProgram(context_, clProgramSource_vector_add_);
            else if (compute_mode_ == ComputeModeFormat.kPredictWithFeaturesTest)
                program_ = new ComputeProgram(context_, clProgramSource_dfprocess_);
            else if (compute_mode_ == ComputeModeFormat.kRelease)
                program_ = new ComputeProgram(context_, clProgramSource_predict_);
            else if (compute_mode_ == ComputeModeFormat.kRelease2D)
                program_ = new ComputeProgram(context_, clProgramSource_predict_2d_);
            else if (compute_mode_ == ComputeModeFormat.kTestBreathFrist)
                program_ = new ComputeProgram(context_, clProgramSource_predict_test_breath_first_tree_);     
            else if (compute_mode_ == ComputeModeFormat.kTest2D)
                program_ = new ComputeProgram(context_, clProgramSource_predict_test_2d_);     
            program_.Build(null, null, null, IntPtr.Zero); 
            // end building
            Console.WriteLine("Build success");            
            count_ = 640 * 480;            

            // set up some of the kernel arguments, some of which are set in other functions
            if (compute_mode_ == ComputeModeFormat.kAddVectorTest)
            {
                kernel_ = program_.CreateKernel("AddVectorWithTrees");                
                //commands_ = new ComputeCommandQueue(context_, context_.Devices[0], ComputeCommandQueueFlags.None);                
                a_ = new ComputeBuffer<short>(context_, ComputeMemoryFlags.ReadOnly, count_);
                c_ = new ComputeBuffer<short>(context_, ComputeMemoryFlags.WriteOnly, count_);
                kernel_.SetMemoryArgument(0, a_);
                kernel_.SetMemoryArgument(2, c_);
            }
            else if (compute_mode_ == ComputeModeFormat.kPredictWithFeaturesTest) {
                kernel_ = program_.CreateKernel("DFProcess");                                
            }
            else if (compute_mode_ == ComputeModeFormat.kRelease || compute_mode_ == ComputeModeFormat.kRelease2D || compute_mode_ == ComputeModeFormat.kTestBreathFrist || compute_mode_ == ComputeModeFormat.kTest2D)
            {
                kernel_ = program_.CreateKernel("Predict");
            }
            commands_ = new ComputeCommandQueue(context_, context_.Devices[0], ComputeCommandQueueFlags.None);                
            Console.WriteLine("Sucessfully create kernel");
        }

        // load the random forest (a bunch of trees) from host memory to GPU memory, including some meta information
        public void LoadTrees(int[] toLoadTrees, short nclasses=0, short ntrees=0, int nfeatures=0) {            
            trees_ = new ComputeBuffer<int>(context_, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, toLoadTrees);
            //commands_.WriteToBuffer(toLoadTrees, trees_, true, null);           
            kernel_.SetMemoryArgument(1, trees_);

            if (compute_mode_ == ComputeModeFormat.kPredictWithFeaturesTest || compute_mode_ == ComputeModeFormat.kRelease || compute_mode_ == ComputeModeFormat.kTestBreathFrist || compute_mode_ == ComputeModeFormat.kTest2D || compute_mode_ == ComputeModeFormat.kRelease2D)
            {
                short[] host_meta_tree = new short[2];
                host_meta_tree[0] = nclasses;
                host_meta_tree[1] = ntrees;                
                meta_tree_ = new ComputeBuffer<short>(context_, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, host_meta_tree);
                kernel_.SetMemoryArgument(0, meta_tree_);
                if (compute_mode_ == ComputeModeFormat.kPredictWithFeaturesTest)
                {
                    x_ = new ComputeBuffer<short>(context_, ComputeMemoryFlags.ReadOnly, nfeatures);
                    kernel_.SetMemoryArgument(2, x_);
                }
                else if (compute_mode_ == ComputeModeFormat.kRelease || compute_mode_ == ComputeModeFormat.kTestBreathFrist || compute_mode_ == ComputeModeFormat.kTest2D || compute_mode_ == ComputeModeFormat.kRelease2D)
                { 
                    // load offset. Is done in LoadOffsets()                    
                }
                if (compute_mode_ == ComputeModeFormat.kPredictWithFeaturesTest)
                {
                    y_ = new ComputeBuffer<float>(context_, ComputeMemoryFlags.WriteOnly, nclasses);                    
                }
                else if (compute_mode_ == ComputeModeFormat.kRelease || compute_mode_ == ComputeModeFormat.kTestBreathFrist || compute_mode_ == ComputeModeFormat.kTest2D || compute_mode_ == ComputeModeFormat.kRelease2D)
                {
                    y_ = new ComputeBuffer<float>(context_, ComputeMemoryFlags.WriteOnly, count_ * nclasses);                    
                }
                // the following bug takes me a day to find out
                kernel_.SetMemoryArgument(3, y_);
                if (compute_mode_ == ComputeModeFormat.kRelease || compute_mode_ == ComputeModeFormat.kTestBreathFrist || compute_mode_ == ComputeModeFormat.kTest2D || compute_mode_ == ComputeModeFormat.kRelease2D)
                {
                    depth_ = new ComputeBuffer<short>(context_, ComputeMemoryFlags.ReadOnly, count_);
                    kernel_.SetMemoryArgument(4, depth_);
                }
            }
        }

        public void LoadOffsets(int[] to_load_offset_list)
        {
            if (compute_mode_ == ComputeModeFormat.kRelease || compute_mode_ == ComputeModeFormat.kTestBreathFrist || compute_mode_ == ComputeModeFormat.kTest2D || compute_mode_ == ComputeModeFormat.kRelease2D)
            {                
                offset_list_ = new ComputeBuffer<int>(context_, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, to_load_offset_list);
                //commands_.WriteToBuffer(to_load_offset_list, offset_list_, true, null);
                kernel_.SetMemoryArgument(2, offset_list_);
                 
                Console.WriteLine("Successfully load offset pair into GPU memory");                
            }
        }

        public void PredictFeatureVector(short [] feature_vector, ref float[] predict_output) 
        {
            commands_.WriteToBuffer(feature_vector, x_, true, null);
            Console.WriteLine("Copy the feature_vector from host to CPU");
            commands_.Execute(kernel_, null, new long[] { 1}, null, null); // set the work-item size here.
            commands_.Finish();
            //predict_output = new float[3];
            commands_.ReadFromBuffer(y_, ref predict_output, true, null);
            //Console.WriteLine("internal GPU output: y[0]: {0}, y[1]: {1}, y[2]:{2}", predict_output[0], predict_output[1], predict_output[2]);
        }

        public void Predict(short[] depth, ref float[] predict_output)
        {
            //Console.WriteLine("array depth dimension: {0}, count: {1}", depth.Length, count_);
            commands_.WriteToBuffer(depth, depth_, true, null);
            //Console.WriteLine("Successfuly write depth to GPU memory");
            if (compute_mode_ == ComputeModeFormat.kRelease || compute_mode_ == ComputeModeFormat.kTestBreathFrist )
                commands_.Execute(kernel_, null, new long[] {count_}, null, null); // set the work-item size to be 640*480.
            else if (compute_mode_ == ComputeModeFormat.kTest2D || compute_mode_ == ComputeModeFormat.kRelease2D)
                commands_.Execute(kernel_, null, new long[] { 640, 480 }, null, null); // set the work-item size to be 640*480.
            commands_.Finish();
            //Console.WriteLine("Successfuly execute the kernel");
            commands_.ReadFromBuffer(y_, ref predict_output, true, null);
        }

        // test function for GPU, when using it also needs to load the tree array (just for test, can have errors)
        public void AddVectorTest( short [] input_array, ref short [] output_array)
        {
            commands_.WriteToBuffer(input_array, a_, true, null);            
            commands_.Execute(kernel_, null, new long[] {count_ }, null, null); // set the work-item size here.
            commands_.Finish();
            commands_.ReadFromBuffer(c_, ref output_array, true, null);            
        }


    }
}




