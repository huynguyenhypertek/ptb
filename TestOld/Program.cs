using System;
using OpenCvSharp;
class Program {
    static void Main() {
        var capture = new VideoCapture(0);
        Console.WriteLine($"Index 0 Opened: {capture.IsOpened()}");
        var capture1 = new VideoCapture(1);
        Console.WriteLine($"Index 1 Opened: {capture1.IsOpened()}");
    }
}
