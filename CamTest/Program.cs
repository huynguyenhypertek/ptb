using System;
using OpenCvSharp;

class Program
{
    static void Main()
    {
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"Trying index {i} with AVFoundation...");
            using var cap = new VideoCapture(i, VideoCaptureAPIs.AVFOUNDATION);
            if (cap.IsOpened()) {
                Console.WriteLine($"SUCCESS: Index {i} opened (AVFoundation). W={cap.FrameWidth} H={cap.FrameHeight}");
            } else {
                Console.WriteLine($"FAILED: Index {i} (AVFoundation).");
            }
        }
    }
}
