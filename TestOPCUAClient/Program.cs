using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Siemens.OPUAClient;

namespace TestOPCUAClient
{
    class Program
    {
        private static ClientAsService opcuaClient;
        private static ApplicationConfiguration config = new ApplicationConfiguration();
        private static Task writer;
        private static ManualResetEvent stop = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            SetupOpcConfiguration();
            opcuaClient = new ClientAsService(ServerType.ServerA, config, "opc.tcp://localhost:51210/UA/SampleServer");
            opcuaClient.ConnectionStateChanged += OpcuaClient_ConnectionStateChanged;
            opcuaClient.Connect();

            writer = new Task(new Action(WriteTest));
            writer.Start();

            Console.WriteLine("trying to connect: enter to stop");
            Console.ReadLine();

            opcuaClient.DisConnect();

        }

        private static void WriteTest()
        {
            double value = 2.000;
            while (!stop.WaitOne(5000))
            {
                if (opcuaClient.Connected)
                    WriteSetpoint(value);
                value += 0.1;
            }
        }

        private static void OpcuaClient_ConnectionStateChanged(bool state, bool browseresult)
        {
            Console.WriteLine($"ConnectionState {state} browseresult: {browseresult}");
            if (browseresult && state)
            {
                BrowseEnsubscribe();
            }
            else
            {
                Console.WriteLine("Reconnect");
                opcuaClient.Connect();
            }
        }


        /// <summary>
        /// Browse en subscribe de data in de opc server
        /// </summary>
        /// <param name="clienttoserver"></param>
        static void BrowseEnsubscribe()
        {
            try
            {

                //Amerongen/BedienplekBCA1/
                string pathToFC = string.Format("Boiler #1.FC1001.Measurement");

                // bepaal hoofd folders
                var resultBoiler = opcuaClient.MainFolders.Find(e => e.DisplayName == "Boilers");

                // Deze bepaald de browse path naar de camera
                if (resultBoiler != null)
                {
                    Subscription serverSubscription = new Subscription();
                    serverSubscription.PublishingEnabled = true;
                    serverSubscription.PublishingInterval = 1000;
                    serverSubscription.Priority = 1;
                    serverSubscription.KeepAliveCount = 10;
                    serverSubscription.LifetimeCount = 20;
                    serverSubscription.MaxNotificationsPerPublish = 1000;
                    opcuaClient._OPCSession.AddSubscription(serverSubscription);
                    serverSubscription.Create();

                    NamespaceTable wellKnownNamespaceUris = new NamespaceTable();
                    wellKnownNamespaceUris.Append("http://opcfoundation.org/Siemens/CCTV");

                    Console.WriteLine("Subscribe to ns{0}:{1}", 3, pathToFC);

                    // this filter trigger on timestamp
                    DataChangeFilter dfilter = new DataChangeFilter();
                    dfilter.Trigger = DataChangeTrigger.StatusValueTimestamp;


                    MonitoredItem item = new MonitoredItem();
                    item.Filter = dfilter;
                    item.DisplayName = pathToFC;
                    item.StartNodeId = new NodeId(1274, 4);
                    item.Notification += Item_Notification;
                    serverSubscription.AddItem(item);

                    serverSubscription.AddItem(item);


                    serverSubscription.ApplyChanges();

                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Subscription error {0}", ex.Message);
            }
        }

        private static void WriteSetpoint(double value)
        {
            try
            {
                WriteValueCollection nodes = new WriteValueCollection();
                // Camera ID
                WriteValue camera = new WriteValue();
                camera.NodeId = new NodeId(1275, 4);
                camera.AttributeId = Attributes.Value;
                DataValue val = new DataValue(new Variant(value));
                camera.Value = val;

                // voeg aaan lijst toe
                nodes.Add(camera);

                // contole
                foreach (WriteValue nodeToWrite in nodes)
                {
                    NumericRange indexRange;
                    ServiceResult result = NumericRange.Validate(nodeToWrite.IndexRange, out indexRange);

                    if (ServiceResult.IsGood(result) && indexRange != NumericRange.Empty)
                    {
                        // apply the index range.
                        object valueToWrite = nodeToWrite.Value.Value;

                        result = indexRange.ApplyRange(ref valueToWrite);

                        if (ServiceResult.IsGood(result))
                        {
                            nodeToWrite.Value.Value = valueToWrite;
                        }
                    }
                }

                // schrijf
                opcuaClient.Write(nodes);

            }
            catch (Exception opException)
            {

                Console.WriteLine($"OPCexception {opException.Message}");
            }

        }

        private static void Item_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            Console.WriteLine("SetCamera item changed DisplayName:{0} sourceTime:{1} Statuscode:{2} Value:{3} wrappedVal:{4}",
                monitoredItem.DisplayName,
                ((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.SourceTimestamp,
                ((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.StatusCode,
                ((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.Value,
                ((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.WrappedValue.Value);

            WriteSetpoint(2.000);


        }

        static void SetupOpcConfiguration()
        {
            // load the application configuration.
            config.ApplicationName = "UnityClient";
            config.ApplicationType = ApplicationType.Client;
            config.ApplicationUri = "urn:localhost: www.siemens.com:UnityClient";
            config.ProductUri = "uri:www.siemens.com:UnityClient";
            config.CertificateValidator = new CertificateValidator();
            config.Extensions = new XmlElementCollection();
            config.SecurityConfiguration = new SecurityConfiguration();
            config.SecurityConfiguration.ApplicationCertificate = new CertificateIdentifier();
            config.SecurityConfiguration.ApplicationCertificate.StoreType = "Directory";
            config.SecurityConfiguration.ApplicationCertificate.StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault";
            config.SecurityConfiguration.ApplicationCertificate.SubjectName = "VaarseinClient";
            config.SecurityConfiguration.TrustedPeerCertificates = new CertificateTrustList();
            config.SecurityConfiguration.TrustedPeerCertificates.StoreType = "Directory";
            config.SecurityConfiguration.TrustedPeerCertificates.StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications";
            config.SecurityConfiguration.TrustedIssuerCertificates = new CertificateTrustList();
            config.SecurityConfiguration.TrustedIssuerCertificates.StoreType = "Directory";
            config.SecurityConfiguration.TrustedIssuerCertificates.StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities";
            config.SecurityConfiguration.RejectedCertificateStore = new CertificateTrustList();
            config.SecurityConfiguration.RejectedCertificateStore.StoreType = "Directory";
            config.SecurityConfiguration.RejectedCertificateStore.StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates";
            config.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
            config.TransportQuotas = new TransportQuotas();
            config.TransportQuotas.OperationTimeout = 600000;
            config.TransportQuotas.MaxStringLength = 1048576;
            config.TransportQuotas.MaxByteStringLength = 1048576;
            config.TransportQuotas.MaxArrayLength = 65535;
            config.TransportQuotas.MaxMessageSize = 4194304;
            config.TransportQuotas.MaxBufferSize = 65535;
            config.TransportQuotas.ChannelLifetime = 300000;
            config.TransportQuotas.SecurityTokenLifetime = 3600000;
            config.ClientConfiguration = new ClientConfiguration();
            config.ClientConfiguration.DefaultSessionTimeout = 60000;
            config.ClientConfiguration.WellKnownDiscoveryUrls.Add("opc.tcp://localhost:4841");
            config.ClientConfiguration.WellKnownDiscoveryUrls.Add("http://{0}:52601/UADiscovery");
            config.ClientConfiguration.WellKnownDiscoveryUrls.Add("http://{0}/UADiscovery/Default.svc");
            config.ClientConfiguration.EndpointCacheFilePath = "UnityClient.Endpoints.xml";
            config.ClientConfiguration.MinSubscriptionLifetime = 10000;


        }

    }
}
