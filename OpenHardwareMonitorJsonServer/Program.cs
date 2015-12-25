using Newtonsoft.Json;
using OpenHardwareMonitor.Hardware;
using System;
using System.Net;
using System.Text;

namespace OpenHardwareMonitorJsonServer
{
    // This is a copy-paste of the Visitor from OpenHardwareMonitor's GUI project.
    class Visitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }

        public void VisitParameter(IParameter parameter) { }
        public void VisitSensor(ISensor sensor) { }
    }

    class Program
    {
        public static void Main(string[] args)
        {
            // Create a computer instance with all sensors enabled.
            var computer = new Computer()
            {
                MainboardEnabled = true,
                CPUEnabled = true,
                RAMEnabled = true,
                GPUEnabled = true,
                FanControllerEnabled = true,
                HDDEnabled = true,
            };

            // Initialize the sensors.
            computer.Open();
            // Start the HTTP server on a separate thread.
            StartServer(computer);

            Console.WriteLine("Listening on http://localhost:8080");
            Console.WriteLine("Press enter to quit...");
            Console.ReadLine();
        }

        private async static void StartServer(Computer computer)
        {
            var visitor = new Visitor();

            // Start a HTTP server on port 8080.
            var listener = new HttpListener();
            listener.Prefixes.Add("http://*:8080/");
            listener.Start();

            // While the server is still running...
            while (listener.IsListening)
            {
                // Context is an incoming HTTP request.
                var context = await listener.GetContextAsync();
                // Update the sensors.
                computer.Accept(visitor);
                // Serialize the data in computer.Hardware into a JSON string, ignoring circular references.
                // NOTE: The circular reference is from each node having a reference to its parent.
                // TODO: The data can be cleaned up some before serializing, it contains a bunch of extraneous data.
                // The serializer in OpenHardwareMonitor's GUI library used by its own JSON server simply collects
                // the values to serialize. It can serve as a starting point.
                var settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                var data = JsonConvert.SerializeObject(computer.Hardware, settings);

                var buffer = Encoding.UTF8.GetBytes(data);
                var response = context.Response;

                try
                {
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                catch (HttpListenerException)
                {
                    // Don't do anything, this is most likely caused by the client ending the TCP connection, and
                    // therefore writing to the closed TCP connection will throw an exception.
                }

                response.OutputStream.Close();
                response.Close();
            }
        }
    }
}
