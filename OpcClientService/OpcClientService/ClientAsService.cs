/***************************************************************
* $Id: OnvifPTZ.cs 4281 2017-06-13 21:30:29Z marcel.de.vreugd $
* $Revision: 4281 $
* $Author: marcel.de.vreugd $
* $Date: 2017-06-13 23:30:29 +0200 (di, 13 jun 2017) $
*
* Omschrijving: 
* Deze module verzorgt de OPC client functionaliteit 
* 
* ======================================================================*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using System.IO;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.Controls;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// M.A de Vreugd 
/// Empty template for an client
/// Client runs aas a console app
/// From a console app it is very easy to port it to:
/// *) Forms app
/// *) Universal app
/// *) Windows Service
/// </summary>

namespace Siemens.OPUAClient
{
   /// <summary>
   /// M.A de Vreugd
   /// deze module maakt verbinding met de betreffende OPC server
   /// in de constructor geef je op welke
   /// </summary>
   /// 
   public delegate void ConnectionState(bool state, bool browseresult);

   // server redu
   public enum ServerType
   {
      ServerA,
      ServerB,
   }


   public class ClientAsService
   {
      // Client configuration xxx.config.xml
      ApplicationConfiguration _configuration;

      // OPC UA session is there when a connection is active
      Session _session;

      // Connector: ued to connect to server, results in an active session 
      ConnectServerCtrl _connectServerCtrl = new ConnectServerCtrl();

      // Node browser: used to get opc nodes from server
      Browser _browser = new Browser();

      // Connector task is repeated until a active session is returned. After tht it is no longer needed
      // After that moment the connector control will wake over the active session
      // When the connection is lost the session is invalid and the connector control will try edlessly to get it back
      ManualResetEvent _connected = new ManualResetEvent(false);

      ServerType _type = ServerType.ServerA;

      // Initial connector is used only once
      // but it will endlessly try to create the first session
      // every interval mseconds
      int _initialConnectorInterval = 30000;

      // Url to server
      string _Serverurl = "";

      // SyncRoot for shared object between threads
      object _opcSessionSync = new object();

      // list met hoofd folders in Object root folder
      ReferenceDescriptionCollection _mainfolders;

      // events
      public event ConnectionState ConnectionStateChanged;

      public ClientAsService(ServerType typ, ApplicationConfiguration configuration, string url, bool usesecurity = false)
      {
         Trace.TraceInformation("Init OPC UA client serverurl {0} usesecurity {1} ServerType:{2}", url, usesecurity, Enum.GetName(typeof(ServerType), typ));

         _type = typ;

         _Serverurl = url;

         _connectServerCtrl.Configuration = _configuration = configuration;
         _connectServerCtrl.ServerUrl = url;
         _connectServerCtrl.UseSecurity = usesecurity;

         // event handlers
         _connectServerCtrl.ConnectComplete += _connectServerCtrl_ConnectComplete;
         _connectServerCtrl.Disposed += _connectServerCtrl_Disposed;
         _connectServerCtrl.ReconnectStarting += _connectServerCtrl_ReconnectStarting;
         _connectServerCtrl.ReconnectComplete += _connectServerCtrl_ReconnectComplete;

         // browser
         _browser.MoreReferences += _browser_MoreReferences;

      }

      private void _browser_MoreReferences(Browser sender, BrowserEventArgs e)
      {
         Trace.TraceInformation("Browser: There are more references aantal: {0}", e.References.Count);
      }

      private void _connectServerCtrl_ReconnectComplete(object sender, EventArgs e)
      {
         Trace.TraceInformation("OPC UA Client _connectServerCtrl_ReconnectComplete args {0}", e);
         try
         {
            _OPCSession = _connectServerCtrl.Session;

            // browse opnieuw
            Browse();
         }
         catch (Exception rc)
         {
            Trace.TraceError("RE-Connection error wit server {0} error {1}", _Serverurl, rc.Message);
            // beter markeren als onbetrouwbaar, bij gebruik op null controleren
            _OPCSession = null;
         }
      }

      private void _connectServerCtrl_ReconnectStarting(object sender, EventArgs e)
      {
         Trace.TraceInformation("OPC UA Client _connectServerCtrl_ReconnectComplete args {0}", e);

         // melden 
         ConnectionChangedEvent(false, false);

         // beter markeren als onbetrouwbaar, bij gebruik op null controleren
         _OPCSession = null;
      }

      private void _connectServerCtrl_Disposed(object sender, EventArgs e)
      {
         Trace.TraceInformation("OPC UA Client _connectServerCtrl_ReconnectStarting args {0}", e);
         // beter markeren als onbetrouwbaar, bij gebruik op null controleren
         _OPCSession = null;
      }

      private void _connectServerCtrl_ConnectComplete(object sender, EventArgs e)
      {
         Trace.TraceInformation("OPC UA Client _connectServerCtrl_ConnectComplete args {0}", e);
         try
         {
            if (_connectServerCtrl.Session != null)
            {
               _OPCSession = _connectServerCtrl.Session;

               // browse the instances in the server.
               //BrowseCTRL.Initialize(m_session, ObjectIds.ObjectsFolder, ReferenceTypeIds.Organizes, ReferenceTypeIds.Aggregates);
               Browse();
            }
         }
         catch (Exception exception)
         {
            Trace.TraceError("Connection error wit server {0} error {1}", _Serverurl, exception.Message);
            _OPCSession = null;
         }

      }


      /// <summary>
      /// Initial connector: will try to create a valid session (every interval mseconds)
      /// When a valid session is created for the first time, 
      /// the initialconnector is no longer needed.
      /// When the session is lost the connectorCtrl will endlessly try to create a new session to the known server
      /// </summary>
      void InitialConnector()
      {
         // First attempt
         Connector();

         while (!_connected.WaitOne(_initialConnectorInterval))
         {
            if (_OPCSession == null)
            {
               Connector();
            }
            else
            {
               // stop me
               _connected.Set();
            }
         }
      }

      /// <summary>
      /// Connect to OPC UA Server
      /// </summary>
      public void Connect()
      {
         try
         {
            // keep connector awake
            _connected.Reset();

            // Assume server is down and keep trying until a valid session
            Task InitialConnectorTask = new Task(new System.Action(InitialConnector));
            InitialConnectorTask.Start();

         }
         catch (Exception ce)
         {
            Trace.TraceError("OPC UA Client error connecting with server {0} error {1}", _Serverurl, ce.Message);
         }
      }

      /// <summary>
      /// Connect to OPC UA Server
      /// </summary>
      void Connector()
      {
         try
         {
            // try connect now
            _connectServerCtrl.Connect();

         }
         catch (Exception ce)
         {
            Trace.TraceError("OPC UA Client error connecting with server {0} error {1}", _Serverurl, ce.Message);
         }
      }


      /// <summary>
      /// DisConnect from server
      /// </summary>
      public void DisConnect()
      {
         try
         {
            // stop connectors
            _connected.Set();

            _connectServerCtrl.Disconnect();
         }
         catch (Exception ce)
         {
            Trace.TraceError("OPC UA Client error DISconnecting with server {0} error {1}", _Serverurl, ce.Message);
         }
      }

      private void Browse()
      {
         // De session is er alleen bij een werkzame connectie
         if (_browser.Session != null)
         {
            // nu browsen
            try
            {
               IAsyncResult result = _session.BeginBrowse(null, null, ObjectIds.ObjectsFolder, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Object | (uint)NodeClass.Variable, new AsyncCallback(BrowseResult), null);
               Trace.TraceInformation("Browsing: Begin  ");
            }
            catch (Exception be)
            {
               Trace.TraceError("Browse: Error {0}", be.Message);
            }
         }
      }



      void BrowseResult(IAsyncResult result)
      {
         if (result != null)
         {
            Trace.TraceInformation("Er is een browse resultaat IsCompleted {0}", result.IsCompleted);
            BrowseResultCollection results = new BrowseResultCollection();
            DiagnosticInfoCollection diags = new DiagnosticInfoCollection();
            ResponseHeader header = _session.EndBrowse(result, out results, out diags);
            Trace.TraceInformation("Browse results {0} diags {0}", results.Count, diags.Count);
            foreach (BrowseResult browseresult in results)
            {
               Trace.TraceInformation("browseresult nodes {0} ", browseresult.References.Count);

               // bewaar main folders
               _mainfolders = browseresult.References;


               foreach (ReferenceDescription node in browseresult.References)
               {
                  Trace.TraceInformation("Node naam {0} ID {1} ", node.BrowseName, node.NodeId);
               }
            }
            // event naar subscribers
            ConnectionChangedEvent(_session.Connected, true);
         }
         else
         {
            // event naar subscribers
            ConnectionChangedEvent(_session.Connected);
         }
      }

      /// <summary>
      /// eventhandler notify op connection change
      /// </summary>
      /// <param name="state"></param>
      private void ConnectionChangedEvent(bool state, bool browseresult = false)
      {
         // alleen als er subscribers zijn
         if (ConnectionStateChanged != null)
         {
            foreach (ConnectionState subscriber in ConnectionStateChanged.GetInvocationList())
            {
               // fire and forget
               subscriber.BeginInvoke(state, browseresult, null, null);
            }
         }
      }

      /// <summary>
      /// Schrijf waarden van nodes naar opc server
      /// </summary>
      /// <param name="nodestowrite"></param>
      /// <returns></returns>
      public bool Write(WriteValueCollection nodestowrite)
      {
         // return zaken
         bool result = true;
         StatusCodeCollection results = null;
         DiagnosticInfoCollection diagnosticInfos = null;

         try
         {
            ResponseHeader response = _session.Write(null, nodestowrite, out results, out diagnosticInfos);

            // controleer het resultaat
            ClientBase.ValidateResponse(results, nodestowrite);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodestowrite);

            // check results
            for (int i = 0; i < results.Count; i++)
            {
               if (StatusCode.IsNotGood(results[i]))
               {
                  Trace.TraceWarning("OPC Scrijven mislukt voor {0} foutcode {1}", nodestowrite[i].NodeId, results[i].ToString());
               }
            }

         }
         catch (Exception we)
         {
            result = false;
            Trace.TraceError("Het schrijven van waarden naar de opc server geeft fouten {0}", we.Message);
         }

         return result;
      }

      /// <summary>
      /// Bepaal connection state
      /// </summary>
      public bool Connected
      {
         get
         {
            if (_session != null)
               return _session.Connected;
            else
               return false;
         }
      }

      /// <summary>
      /// De hoofd folders op de server
      /// </summary>
      public ReferenceDescriptionCollection MainFolders { get { return _mainfolders; } }


      /// <summary>
      /// Server a of b
      /// </summary>
      public ServerType Type { get { return _type; } }

      /// <summary>
      /// De session komt en gaat met de connection
      /// </summary>
      public Session _OPCSession
      {
         get
         {
            Session result = null;
            lock (_opcSessionSync)
            {
               result = _session;
            }
            return result;
         }
         set
         {
            lock (_opcSessionSync)
            {
               _session = value;
               _browser.Session = _session;
            }
         }
      }
   }
}
