using System;
using System.Collections;
using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Client;
using Madv113.OPUAClient;
using UnityEngine;

public class OpcUAClient : MonoBehaviour
{
    private ClientAsService opcuaClient;
    private ApplicationConfiguration config = new ApplicationConfiguration();
    private double setPoint = 2;

    // Use this for initialization
    void Start()
    {
        // connect to OPCFoundation SampleServer
        SetupOpcConfiguration();
        opcuaClient = new ClientAsService(ServerType.ServerA, config, "opc.tcp://localhost:51210/UA/SampleServer");
        opcuaClient.ConnectionStateChanged += OpcuaClient_ConnectionStateChanged;
        opcuaClient.Connect();
    }

    void OnDestroy()
    {
        opcuaClient.DisConnect();
    }
    private void OpcuaClient_ConnectionStateChanged(bool state, bool browseresult)
    {
        Debug.Log($"ConnectionState {state} browseresult: {browseresult}");
        if (browseresult && state)
        {
            BrowseEnsubscribe();
        }
        else
        {
            Debug.Log("Reconnect");
            opcuaClient.Connect();
        }
    }

    /// <summary>
    /// Browse en subscribe de data in de opc server
    /// </summary>
    /// <param name="clienttoserver"></param>
    void BrowseEnsubscribe()
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

                Debug.Log($"Subscribe to ns{4}:{pathToFC}");

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
            Debug.Log($"Subscription error {ex.Message}");
        }
    }

    private void Item_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        Debug.Log($"SetCamera item changed DisplayName:{monitoredItem.DisplayName} sourceTime:{((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.SourceTimestamp} Statuscode:{((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.StatusCode} Value:{((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.Value} wrappedVal:{((Opc.Ua.MonitoredItemNotification)monitoredItem.LastValue).Value.WrappedValue.Value}");

        WriteSetpoint(2.000);


    }


    private void WriteSetpoint(double value)
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

            Debug.Log($"OPCexception {opException.Message}");
        }

    }




    // Update is called once per frame
    void Update()
    {
        setPoint += 0.01;
        WriteSetpoint(setPoint);
    }

    void SetupOpcConfiguration()
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
