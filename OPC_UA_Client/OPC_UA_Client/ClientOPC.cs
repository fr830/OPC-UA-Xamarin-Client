﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using OPC_UA_Client.Exceptions;
using OPC_UA_Client.ViewModel;
using Xamarin.Forms;

namespace OPC_UA_Client
{
    public class ClientOPC
    {
        ApplicationInstance application;
        public Session session;
        public EndpointDescriptionCollection endpoints { get; set; }
        public ApplicationConfiguration config;
        public bool haveAppCertificate;
        public uint clientHandle { get; set; }
        

        public ClientOPC()
        {
            session = null;
          
            config = null;
            clientHandle = 0;
        }

        // Recuperare l'elenco degli endpoint esposti dal server
        public ListEndpoint DiscoveryEndpoints(String endpointURL)
        {
            ListEndpoint list = new ListEndpoint();
            var endpointConfiguration = EndpointConfiguration.Create(config);
            try
            {
                Uri uri = new Uri(endpointURL);

                DiscoveryClient discoveryC = DiscoveryClient.Create(uri, endpointConfiguration);

                endpoints = discoveryC.GetEndpoints(null);

                for (int i = 0; i < endpoints.Count; i++)
                {
                    list.endpointList.Add(
                        new EndpointView(endpoints[i].EndpointUrl, endpoints[i].SecurityMode.ToString(), endpoints[i].TransportProfileUri, i, endpoints[i].SecurityPolicyUri.Split('#')[1]));
                }
                return list;
            }
            catch (System.UriFormatException)
            {
                return list = null;
            }
            catch (ServiceResultException p)
            {
                throw new BadConnectException("Impossible to connect to server!", p);
            }
        }

        public async void CreateCertificate()
        {
            application = new ApplicationInstance
            {
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Opc.Ua.Client"
            };
            if (Device.RuntimePlatform == "Android")
            {
                string currentFolder = DependencyService.Get<IPathService>().PublicExternalFolder.ToString();
                string filename = application.ConfigSectionName + ".Config.xml";

                string content = DependencyService.Get<IAssetService>().LoadFile(filename);

                File.WriteAllText(currentFolder + filename, content);
                // load the application configuration.

                config = await application.LoadApplicationConfiguration(currentFolder + filename, false);
            }
            else
            {
                // load the application configuration.
                config = await application.LoadApplicationConfiguration(false);
            }
            // check the application certificate.
            haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            config.ApplicationName = "OPC UA Sample Client";
            if (haveAppCertificate)
            {
                config.ApplicationUri = Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }           
        }

        private void OnKeepAliveHandler(object sender, EventArgs e)
        {
           
            if (session.KeepAliveStopped)

            {
                session = null;
                Console.WriteLine("SESSION CLOSED SEND!");
                MessagingCenter.Send<ClientOPC>(this, "SessionClose");
            }
        }
        

        private void OnStateChangedHandler(object sender, SubscriptionStateChangedEventArgs e)
        {
            var sub = sender as Subscription;
            if (e.Status==SubscriptionChangeMask.Deleted)
            {
                Console.WriteLine("Suscription Deleted SEND!");
                MessagingCenter.Send<ClientOPC>(this, "SubDelete");
            }
        }

        public async Task<SessionView> CreateSessionChannelAsync(int indexEndpoint)
        {
            SessionView sessionView;

            UserIdentity userI = new UserIdentity(new AnonymousIdentityToken());
            var endpoint = new ConfiguredEndpoint(null, endpoints[indexEndpoint]);

           try {
                session = await Task.Run(() =>
                {
                   return Session.Create(config, endpoint, false, "OPC Client", 60000, userI, null);
                   
                });
               
                if (session == null)
                {
                    sessionView = null;
                }
                else
                {
                    session.KeepAlive += OnKeepAliveHandler;
                    EndpointView endpointView = new EndpointView(session.Endpoint.EndpointUrl, session.Endpoint.SecurityMode.ToString(), session.Endpoint.TransportProfileUri, 0, session.Endpoint.SecurityPolicyUri.Split('#')[1]);
                    sessionView = new SessionView(session.SessionId.Identifier.ToString(), session.SessionId.NamespaceIndex.ToString(), session.SessionName, endpointView);
                }
                return sessionView;

            }
          catch (ServiceResultException p)
            {
                throw new UnsupportedEndpointException(p.Message);
                
                }
                    }
        
        public async Task<SessionView> CreateSessionChannelAsync(int indexEndpoint, string username, string password)
        {
            SessionView sessionView;
            UserIdentity userI = new UserIdentity(username, password);
            var endpoint = new ConfiguredEndpoint(null, endpoints[indexEndpoint]);
            try
            {
                session =await Task.Run(() =>
                  {
                   return  Session.Create(config, endpoint, false, "OPC Client", 60000, userI, null);
                });
                if (session == null)
                {
                     sessionView = null;
                }
                else
                {
                    session.KeepAlive += OnKeepAliveHandler;
                    EndpointView endpointView = 
                        new EndpointView(session.Endpoint.EndpointUrl, session.Endpoint.SecurityMode.ToString(), session.Endpoint.TransportProfileUri, 0, session.Endpoint.SecurityPolicyUri.Split('#')[1]);
                    sessionView =
                        new SessionView(session.SessionId.Identifier.ToString(), session.SessionId.NamespaceIndex.ToString(), session.SessionName, endpointView);
                }
                return sessionView;
            }
            catch (ServiceResultException p)
            {
                throw new BadUserException(p.Message, p);
            }
        }
        
        /*TimestampsToReturn*/
        public List<NodeView> readVariable(string identifier, ushort namespaceIndex, Double maxAge, int timestamp, uint attribute)
        {
            NodeId node = new NodeId(identifier, namespaceIndex);
            List<NodeView> nodesRead = new List<NodeView>();
            string reset = "--/--/---- --:--:--";
            DataValueCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;
            ReadValueId nodeToRead = new ReadValueId
            {
                NodeId = node,
                AttributeId = attribute,
                IndexRange = null, 
                DataEncoding = null
            };
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
            nodesToRead.Add(nodeToRead);
            TimestampsToReturn t = TimestampsToReturn.Invalid;
            switch (timestamp)
            {
                case 0:
                    t = TimestampsToReturn.Source;
                    break;
                case 1:
                    t = TimestampsToReturn.Server;
                    break;
                case 2:
                    t = TimestampsToReturn.Both;
                    break;
                case 3:
                    t = TimestampsToReturn.Neither;
                    break;
            }
            try
            {
                session.Read(null, maxAge, t, nodesToRead, out results, out diagnosticInfos);
                foreach (DataValue result in results)
                {
                    switch (timestamp)
                    {
                        case 0:
                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), result.SourceTimestamp.ToString(), reset));
                            break;
                        case 1:
                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), reset, result.ServerTimestamp.ToString()));
                            break;
                        case 2:
                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), result.SourceTimestamp.ToString(), result.ServerTimestamp.ToString()));
                            break;
                        case 3:
                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), reset, reset));
                            break;
                    }
                }
            }
            catch (NullReferenceException p)
            {
                throw new NoNodeToReadException("Node not found", p);
            }
            return nodesRead;
        }

        public List<NodeView> readVariable(UInt32 identifier, ushort namespaceIndex, Double maxAge, int timestamp, uint attribute)
        {
            NodeId node = new NodeId(identifier, namespaceIndex);
            List<NodeView> nodesRead = new List<NodeView>();

            DataValueCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;
            ReadValueId nodeToRead = new ReadValueId
            {

                NodeId = node,
                AttributeId = attribute,
                IndexRange = null, // Da aggiungere al metodo dopo 
                DataEncoding = null // da aggiungere al metodo dopo
            };

            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
            nodesToRead.Add(nodeToRead);
            TimestampsToReturn t = TimestampsToReturn.Invalid;
            switch (timestamp)
            {
                case 0:
                    t = TimestampsToReturn.Source;
                    break;
                case 1:
                    t = TimestampsToReturn.Server;
                    break;
                case 2:
                    t = TimestampsToReturn.Both;
                    break;
                case 3:
                    t = TimestampsToReturn.Neither;
                    break;
            }
            try
            {
                session.Read(null, maxAge, t, nodesToRead, out results, out diagnosticInfos);
                string reset = "--/--/---- --:--:--";
                foreach (DataValue result in results)
                {
                    switch (timestamp)
                    {
                        case 0:

                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), result.SourceTimestamp.ToString(), reset));

                            break;
                        case 1:
                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), reset, result.ServerTimestamp.ToString()));

                            break;
                        case 2:
                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), result.SourceTimestamp.ToString(), result.ServerTimestamp.ToString()));
                           
                            break;
                        case 3:
                            nodesRead.Add(new NodeView(result.Value.ToString(), result.StatusCode.ToString(), reset, reset));
                           
                            break;
                    }


                }
            }
            catch (NullReferenceException p)
            {

                throw new NoNodeToReadException("Node not found", p);

            }


            return nodesRead;
        }

        public List<String> WriteVariable( string identifier, ushort namespaceIndex, Object value, uint attribute)
        {
            NodeId node = null;
            List<String> statusCodeWrite = new List<String>();
                    node = new NodeId(identifier, namespaceIndex);
            DataValue valueToWrite = new DataValue()
            {
                Value = (new Variant(value))
            };
            DiagnosticInfoCollection diagnosticInfos = null;
            WriteValueCollection nodesTowrite = new WriteValueCollection();
            WriteValue nodeToWrite = new WriteValue()
            {
                NodeId = node,
                AttributeId = attribute,
                Value = valueToWrite,
                IndexRange = null 
            };
            nodesTowrite.Add(nodeToWrite);
            StatusCodeCollection writeResults;
            session.Write(null, nodesTowrite, out writeResults, out diagnosticInfos);
            for (int i = 0; i < writeResults.Count; i++)
            {
                statusCodeWrite.Add(writeResults[i].ToString());
            }
            return statusCodeWrite;
        }

        public List<String> WriteVariable( uint identifier, ushort namespaceIndex, Object value, uint attribute)
        {
            NodeId node = null;
            List<String> statusCodeWrite = new List<String>();

            

                node = new NodeId(identifier, namespaceIndex);
            

            DataValue valueToWrite = new DataValue()
            {
                Value = (new Variant(value))

            };

            DiagnosticInfoCollection diagnosticInfos = null;
            WriteValueCollection nodesTowrite = new WriteValueCollection();

            WriteValue nodeToWrite = new WriteValue()
            {

                NodeId = node,
                AttributeId = attribute,
                Value = valueToWrite,
                IndexRange = null // da aggiungere al metodo dopo
            };

            nodesTowrite.Add(nodeToWrite);
            StatusCodeCollection writeResults;
            session.Write(null, nodesTowrite, out writeResults, out diagnosticInfos);
            for (int i = 0; i < writeResults.Count; i++)
            {
                statusCodeWrite.Add(writeResults[i].ToString());
            }
            return statusCodeWrite;
        }

        private void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = config.SecurityConfiguration.AutoAcceptUntrustedCertificates;
            }
        }

        //Create subscriptions
        public SubscriptionView CreateSub(double requestedPublishingInterval, uint requestedLifeTimeCount, uint requestedKeepAliveCount, 
            uint MaxNotificationPerPublish, bool _PublishingEnabled, byte Priority)
        {
            SubscriptionView sub;
            Subscription subscription = new Subscription()
            {
                KeepAliveCount = requestedKeepAliveCount,
                LifetimeCount = requestedLifeTimeCount,
                MaxNotificationsPerPublish = MaxNotificationPerPublish,
                Priority = Priority,
                PublishingInterval = Convert.ToInt32(requestedPublishingInterval),
                PublishingEnabled = _PublishingEnabled
            };
            subscription.StateChanged += OnStateChangedHandler;
            //Aggiunge la subscription al campo subscriptions della sessione
            session.AddSubscription(subscription);
            /*
             * La create comunica con il server e crea effettivamente la subscription, salvando nei campi current
             * i valori revised (PublishingInterval, KeepAliveCount, LifetimeCount) 
            */
            subscription.Create();
            sub = 
                new SubscriptionView(subscription.Id, subscription.CurrentPublishingInterval, subscription.CurrentLifetimeCount, 
                subscription.CurrentKeepAliveCount, subscription.MaxNotificationsPerPublish, 
                subscription.PublishingEnabled, subscription.CurrentPriority);
            return sub;
        }

        public List<SubscriptionView> GetSubscriptionViews()
        {
            List<SubscriptionView> listSubView = new List<SubscriptionView>();
            IEnumerable<Subscription> collectionSubscription = session.Subscriptions;

            foreach (Subscription sub in collectionSubscription)
            {
                listSubView.Add(
                    new SubscriptionView(sub.Id, sub.CurrentPublishingInterval, sub.CurrentLifetimeCount, sub.CurrentKeepAliveCount, 
                    sub.MaxNotificationsPerPublish, sub.PublishingEnabled, sub.Priority));
            }
            return listSubView;
        }
        
        //Funzione che permette di recuperare la subscription a cui bisogna aggiungere il monitoredItem
        public Subscription GetSubscription(uint subscriptionId)
        {
            foreach (Subscription sub in session.Subscriptions)
            {
                if (sub.Id == subscriptionId)
                    return sub;
            }
            return null;
        }

        // string nodeClass: Parametro per gestire MonitoredItem con NodeClass diversa da Variable
        public MonitoredItemView CreateMonitoredItem(uint subscriptionId, ushort namespaceIndex, uint identifierNode, 
            int samplingInterval, bool discardOldest, uint queueSize, 
            int monitoringMode, int filterTrigger, uint deadbandType, double deadbandValue)
        {
            Subscription sub = GetSubscription(subscriptionId);
            NodeId node;
            //Initialize Filter Parameters
            DataChangeTrigger _trigger;
            string filterTriggerView;
            switch (filterTrigger)
            {
                case 0:
                    _trigger = DataChangeTrigger.Status;
                    filterTriggerView = "Status";
                    break;
                case 1:
                    _trigger = DataChangeTrigger.StatusValue;
                    filterTriggerView = "StatusValue";
                    break;
                case 2:
                    _trigger = DataChangeTrigger.StatusValueTimestamp;
                    filterTriggerView = "StatusValueTimestamp";
                    break;
                default:
                    _trigger = DataChangeTrigger.StatusValue;
                    filterTriggerView = "StatusValue";
                    break;
            }
            string deadbandTypeView;
            switch (deadbandType)
            {
                case 0:
                    deadbandTypeView = "None";
                    break;
                case 1:
                    deadbandTypeView = "Absolute";
                    break;
                case 2:
                    deadbandTypeView = "Percent";
                    break;
                default:
                    deadbandTypeView = null;
                    break;
            }
            DataChangeFilter filter = new DataChangeFilter()
            {
                Trigger = _trigger,
                DeadbandType = deadbandType,
                DeadbandValue = deadbandValue
            };
            //Initialize Monitored Item Parameters
            MonitoringMode _monitoringMode;
            switch (monitoringMode)
            {
                case 0:
                    _monitoringMode = MonitoringMode.Disabled;
                    break;
                case 1:
                    _monitoringMode = MonitoringMode.Sampling;
                    break;
                case 2:
                    _monitoringMode = MonitoringMode.Reporting;
                    break;
                default:
                    _monitoringMode = MonitoringMode.Reporting;
                    break;
            }
           
             node = new NodeId(identifierNode, namespaceIndex);
           try { 
                    session.ReadNode(node);
                    }
                    catch (ServiceResultException )
                    {
                       throw new NoNodeToReadException("Node not found!");
                    }   
            MonitoredItem monitoredItem = new MonitoredItem(clientHandle)
            {
                AttributeId = Attributes.Value,
                DiscardOldest = discardOldest,
                Filter = filter,
                MonitoringMode = _monitoringMode,
                NodeClass = NodeClass.Variable,
                QueueSize = queueSize,
                SamplingInterval = samplingInterval,
                StartNodeId = node
            };
            clientHandle++; //Identifier di un singolo monitored item --> univoco solo all'interno della subscription
            monitoredItem.Notification += new MonitoredItemNotificationEventHandler(OnNotificationItem);
            //Aggiunge l'item tra i monitored items della subscription senza crearlo
            sub.AddItem(monitoredItem);
            //Se aggiungiamo altri monitoredItem la funzione successiva li creerà tutti
            //Comunica con il server e crea effettivamente il monitoredItem
            IList<MonitoredItem> createdMonitoredItems = sub.CreateItems();
            sub.ApplyChanges();
           //Questa funzione ritorna la lista dei monitoredItems creati al momento della chiamata
            return new MonitoredItemView(monitoredItem.ClientHandle, monitoredItem.ResolvedNodeId.NamespaceIndex, monitoredItem.ResolvedNodeId.Identifier.ToString(), subscriptionId, monitoredItem.SamplingInterval, filterTriggerView, deadbandTypeView, deadbandValue);
        }

        public MonitoredItemView CreateMonitoredItem(uint subscriptionId, ushort namespaceIndex, string identifierNode, int samplingInterval, bool discardOldest, uint queueSize, int monitoringMode, int filterTrigger, uint deadbandType, double deadbandValue)
        {

            Subscription sub = GetSubscription(subscriptionId);
            NodeId node;
            //Initialize Filter Parameters
            DataChangeTrigger _trigger;
            string filterTriggerView;
            switch (filterTrigger)
            {
                case 0:
                    _trigger = DataChangeTrigger.Status;
                    filterTriggerView = "Status";
                    break;
                case 1:
                    _trigger = DataChangeTrigger.StatusValue;
                    filterTriggerView = "StatusValue";
                    break;
                case 2:
                    _trigger = DataChangeTrigger.StatusValueTimestamp;
                    filterTriggerView = "StatusValueTimestamp";
                    break;
                default:
                    _trigger = DataChangeTrigger.StatusValue;
                    filterTriggerView = "StatusValue";
                    break;
            }

            string deadbandTypeView;
            switch (deadbandType)
            {
                case 0:
                    deadbandTypeView = "None";
                    break;
                case 1:
                    deadbandTypeView = "Absolute";
                    break;
                case 2:
                    deadbandTypeView = "Percent";
                    break;
                default:
                    deadbandTypeView = null;
                    break;

            }

            DataChangeFilter filter = new DataChangeFilter()
            {
                Trigger = _trigger,
                DeadbandType = deadbandType,
                DeadbandValue = deadbandValue
            };

            //Initialize Monitored Item Parameters
            MonitoringMode _monitoringMode;
            switch (monitoringMode)
            {
                case 0:
                    _monitoringMode = MonitoringMode.Disabled;
                    break;
                case 1:
                    _monitoringMode = MonitoringMode.Sampling;
                    break;
                case 2:
                    _monitoringMode = MonitoringMode.Reporting;
                    break;
                default:
                    _monitoringMode = MonitoringMode.Reporting;
                    break;

            }

            //Set NodeId della variabile che si vuole leggere con gestione dell'identifier sia string che integer
            node = new NodeId(identifierNode, namespaceIndex);
            try
            {
                session.ReadNode(node);
            }
            catch (ServiceResultException)
            {throw new NoNodeToReadException("Node not found!");
            }

           MonitoredItem monitoredItem = new MonitoredItem(clientHandle)
            {
                AttributeId = Attributes.Value,
                DiscardOldest = discardOldest,
                Filter = filter,
                MonitoringMode = _monitoringMode,
                NodeClass = NodeClass.Variable,
                QueueSize = queueSize,
                SamplingInterval = samplingInterval,
                StartNodeId = node
            };
            clientHandle++; //Identifier di un singolo monitored item --> univoco solo all'interno della subscription

            monitoredItem.Notification += new MonitoredItemNotificationEventHandler(OnNotificationItem);

            //Aggiunge l'item tra i monitored items della subscription senza crearlo

            sub.AddItem(monitoredItem);

            //Se aggiungiamo altri monitoredItem la funzione successiva li creerà tutti

            //Comunica con il server e crea effettivamente il monitoredItem
            IList<MonitoredItem> createdMonitoredItems = sub.CreateItems();
            sub.ApplyChanges();
             //Questa funzione ritorna la lista dei monitoredItems creati al momento della chiamata

            return new MonitoredItemView(monitoredItem.ClientHandle, monitoredItem.ResolvedNodeId.NamespaceIndex, monitoredItem.ResolvedNodeId.Identifier.ToString(), subscriptionId, monitoredItem.SamplingInterval, filterTriggerView, deadbandTypeView, deadbandValue);
        }
        
        public List<MonitoredItemView> GetMonitoredItemViews(uint subscriptionId)
        {
            List<MonitoredItemView> ItemViews = new List<MonitoredItemView>();

            Subscription sub = GetSubscription(subscriptionId);
            IEnumerable<MonitoredItem> monitoredItems = sub.MonitoredItems;

            foreach (MonitoredItem monitoredItem in monitoredItems)
            {
                DataChangeFilter filter = (DataChangeFilter)monitoredItem.Filter;
                string filterTriggerView;
                switch (filter.Trigger)
                {
                    case DataChangeTrigger.Status:
                        filterTriggerView = "Status";
                        break;
                    case DataChangeTrigger.StatusValue:
                        filterTriggerView = "StatusValue";
                        break;
                    case DataChangeTrigger.StatusValueTimestamp:
                        filterTriggerView = "StatusValueTimestamp";
                        break;
                    default:
                        filterTriggerView = "StatusValue";
                        break;
                }

                string deadbandTypeView;
                switch (filter.DeadbandType)
                {
                    case 0:
                        deadbandTypeView = "None";
                        break;
                    case 1:
                        deadbandTypeView = "Absolute";
                        break;
                    case 2:
                        deadbandTypeView = "Percent";
                        break;
                    default:
                        deadbandTypeView = null;
                        break;

                }

                ItemViews.Add(
                    new MonitoredItemView(monitoredItem.ClientHandle, monitoredItem.ResolvedNodeId.NamespaceIndex, monitoredItem.ResolvedNodeId.Identifier.ToString(), subscriptionId, monitoredItem.SamplingInterval, filterTriggerView, deadbandTypeView, filter.DeadbandValue)
                    );
            }

            return ItemViews;
        }

        public SubscriptionView GetSubscriptionViewById(uint subscriptionId)
        {
            Subscription sub = GetSubscription(subscriptionId);
            return new SubscriptionView(sub.Id, sub.CurrentPublishingInterval, sub.CurrentLifetimeCount, sub.CurrentKeepAliveCount, sub.MaxNotificationsPerPublish, sub.CurrentPublishingEnabled, sub.CurrentPriority);
        }

        private void OnNotificationItem(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            DataChangeView view=new DataChangeView();
            Console.WriteLine("Sono l'item" + item.ClientHandle);
          

            string message = ("update: " + item.ClientHandle);
                foreach (var value in item.DequeueValues())
            {

                Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
                view.ClientHandle = item.ClientHandle;
                view.DisplayName = item.DisplayName;
                view.Value = value.Value.ToString();
                view.SourceTimestamp = value.SourceTimestamp.ToString();
                view.ServerTimestamp = value.ServerTimestamp.ToString();
                view.StatusCode = value.StatusCode.ToString();
             

                MessagingCenter.Send<ClientOPC, DataChangeView>(this, message, view);
               
            }
           


        }

        public Tree GetRootNode()
        {
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;
            Tree browserTree = new Tree();
            try
            {
                session.Browse(
                    null,
                    null,
                    ObjectIds.ObjectsFolder,
                    0u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    0,
                    out continuationPoint,
                    out references);

                browserTree.currentView.Add(
                    new ListNode
                    {
                        Id = ObjectIds.ObjectsFolder.ToString(),
                        NodeName = "Root",
                        Children = (references?.Count != 0)
                    });
                return browserTree;
            }
            catch
            {
                //Disconnect session
                return null;
            }
        }

        public Tree GetChildren(string node)
        {
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;
            Tree browserTree = new Tree();
            try
            {
                session.Browse(
                    null,
                    null,
                    node,
                    0u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    0,
                    out continuationPoint,
                    out references);

                if (references != null)
                {
                    foreach (var nodeReference in references)
                    {
                        ReferenceDescriptionCollection childReferences = null;
                        Byte[] childContinuationPoint;

                        session.Browse(
                            null,
                            null,
                            ExpandedNodeId.ToNodeId(nodeReference.NodeId, session.NamespaceUris),
                            0u,
                            BrowseDirection.Forward,
                            ReferenceTypeIds.HierarchicalReferences,
                            true,
                            0,
                            out childContinuationPoint,
                            out childReferences);

                        INode currentNode = null;
                        try
                        {
                            currentNode = session.ReadNode(ExpandedNodeId.ToNodeId(nodeReference.NodeId, session.NamespaceUris));
                        }
                        catch (Exception)
                        {
                            // skip this node
                            continue;
                        }

                        byte currentNodeAccessLevel = 0;
                        byte currentNodeEventNotifier = 0;
                        bool currentNodeExecutable = false;

                        VariableNode variableNode = currentNode as VariableNode;
                        if (variableNode != null)
                        {
                            currentNodeAccessLevel = variableNode.UserAccessLevel;
                            currentNodeAccessLevel = (byte)((uint)currentNodeAccessLevel & ~0x2);
                        }

                        ObjectNode objectNode = currentNode as ObjectNode;
                        if (objectNode != null)
                        {
                            currentNodeEventNotifier = objectNode.EventNotifier;
                        }

                        ViewNode viewNode = currentNode as ViewNode;
                        if (viewNode != null)
                        {
                            currentNodeEventNotifier = viewNode.EventNotifier;
                        }

                        MethodNode methodNode = currentNode as MethodNode;
                        if (methodNode != null)
                        {
                            currentNodeExecutable = methodNode.UserExecutable;
                        }

                        browserTree.currentView.Add(new ListNode()
                        {
                            Id = nodeReference.NodeId.ToString(),
                            NodeName = nodeReference.DisplayName.Text.ToString(),
                            NodeClass = nodeReference.NodeClass.ToString(),
                            AccessLevel = currentNodeAccessLevel.ToString(),
                            EventNotifier = currentNodeEventNotifier.ToString(),
                            Executable = currentNodeExecutable.ToString(),
                            Children = (references?.Count != 0),
                        });
                    }
                    if (browserTree.currentView.Count == 0)
                    {
                        INode currentNode = session.ReadNode(new NodeId(node));

                        byte currentNodeAccessLevel = 0;
                        byte currentNodeEventNotifier = 0;
                        bool currentNodeExecutable = false;

                        VariableNode variableNode = currentNode as VariableNode;

                        if (variableNode != null)
                        {
                            currentNodeAccessLevel = variableNode.UserAccessLevel;
                            currentNodeAccessLevel = (byte)((uint)currentNodeAccessLevel & ~0x2);
                        }

                        ObjectNode objectNode = currentNode as ObjectNode;

                        if (objectNode != null)
                        {
                            currentNodeEventNotifier = objectNode.EventNotifier;
                        }

                        ViewNode viewNode = currentNode as ViewNode;

                        if (viewNode != null)
                        {
                            currentNodeEventNotifier = viewNode.EventNotifier;
                        }

                        MethodNode methodNode = currentNode as MethodNode;

                        if (methodNode != null)
                        {
                            currentNodeExecutable = methodNode.UserExecutable;
                        }

                        browserTree.currentView.Add(new ListNode()
                        {
                            Id = node,
                            NodeName = currentNode.DisplayName.Text.ToString(),
                            NodeClass = currentNode.NodeClass.ToString(),
                            AccessLevel = currentNodeAccessLevel.ToString(),
                            EventNotifier = currentNodeEventNotifier.ToString(),
                            Executable = currentNodeExecutable.ToString(),
                            Children = false
                        });
                    }
                }
                return browserTree;
            }
            catch
            {
                //Disconnect
                return null;
            }
        }

        public bool CloseSubscription(uint subscriptionId)
        {
            Subscription sub = GetSubscription(subscriptionId);
            return(session.RemoveSubscription(sub));
        }

        public void DeleteMonitoredItem(uint subscriptionId, uint clientHandle)
        {
            Subscription sub = GetSubscription(subscriptionId);
            sub.RemoveItem(sub.FindItemByClientHandle(clientHandle));
            sub.ApplyChanges();
        }
    }

}


