using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenHardwareMonitor.Hardware;
using System;
using System.Net;
using System.Text;
using System.Reflection;

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

    // Json.NET custom converter to output ToString() for the given property.
    class ToStringJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }

    // Json.NET custom contract resolver. Needed in order to use the custom converter, since the custom converter is
    // intended to be used with annotations, and we can't annotate properties in a compiled library.
    class CustomContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            // Don't serialize the Values or Parameters properties. The Values property is an array of all the sensor
            // readings in the past 24 hours, making the JSON size very, very large if enough time has passed. The
            // Parameters property only holds strings on how it calculated the Value property, and can be ignored since
            // it won't ever change.
            if (property.PropertyName == "Values" || property.PropertyName == "Parameters")
            {
                property.ShouldSerialize = (x) => false;
            }

            return property;
        }

        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            JsonObjectContract contract = base.CreateObjectContract(objectType);
            // Use the ToString JSON converter if the type is OpenHardwareMonitor.Hardware.Identifier. Otherwise, the
            // default serialization will serialize it as an object, and since it has no public properties, it will
            // serialize to "Identifier: {}".
            if (objectType == typeof(Identifier))
            {
                contract.Converter = new ToStringJsonConverter();
            }

            return contract;
        }
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
            listener.Prefixes.Add("http://localhost:8080/");
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
                var settings = new JsonSerializerSettings() {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    ContractResolver = new CustomContractResolver()
                };
                var data = JsonConvert.SerializeObject(computer.Hardware, settings);

                var buffer = Encoding.UTF8.GetBytes(data);
                var response = context.Response;

                try
                {
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.AddHeader("Content-Type", "application/json");
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                catch (HttpListenerException)
                {
                    // Don't do anything, this is most likely caused by the client ending the TCP connection, and
                    // therefore writing to the closed TCP connection will throw an exception.
                }

                response.OutputStream.Close();
            }
        }
    }
}
