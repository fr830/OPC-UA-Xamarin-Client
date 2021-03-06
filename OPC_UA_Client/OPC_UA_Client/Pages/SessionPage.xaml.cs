﻿using OPC_UA_Client.Pages;
using OPC_UA_Client.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace OPC_UA_Client
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class SessionPage : ContentPage
	{
        public ClientOPC client;
        public SessionView sessionView;
        public  SessionPage (ClientOPC _client, SessionView _sessionView)
		{
            InitializeComponent();
            client = _client;
            sessionView = _sessionView;
            DisplaySession();
            SubscribePage();
        }

        private void SubscribePage() {
            MessagingCenter.Subscribe<ClientOPC>(this, "SessionClose",
                async (sender) => {
                    
                    await Task.Run(() =>
                    {
                       Device.BeginInvokeOnMainThread(() =>   DisplayAlert("Error", "Session Expired!", "Ok"));
                    });

                    
                    await Task.Run(() =>
                    {
                        Device.BeginInvokeOnMainThread(() => {
                            Page p = new MainPage();
                            p.Title="Client OPC";
                            Navigation.PushAsync(p);
                            foreach (var page in Navigation.NavigationStack.ToList()) {
                                if (page != p) {
                                    Navigation.RemovePage(page);
                                }
                            }
                            MessagingCenter.Unsubscribe<ClientOPC>(this, "SessionClose");
                        });
                    });
                });
        }
 
        private void DisplaySession() {
            NamespaceIndex.Text = sessionView.indexNameSpace;
            Identifier.Text = sessionView.identifier;
            SessionName.Text = sessionView.sessionName;
            EndpointUrl.Text = sessionView.endpointView.endpointURL;
            SecurityMode.Text = sessionView.endpointView.securityMode;
            TransportUri.Text = sessionView.endpointView.transportProfileURI;
            SecurityPolicy.Text = sessionView.endpointView.securityPolicyUri;
        }

        private async void OnRead(object sender, EventArgs e)
        {
            ContentPage readPage = new ReadPage(client);
            readPage.Title = "OPC Read Service";
            await Navigation.PushAsync(readPage);
        }

        private async void OnWrite(object sender, EventArgs e)
        {
            ContentPage writePage = new WritePage(client);
            writePage.Title = "OPC Write Service";
            await Navigation.PushAsync(writePage);

        }

        private async void OnSubscription(object sender, EventArgs e)
        {
            ContentPage subPage = new CreateSubscriptionPage(client);
            subPage.Title = "Subscription Service";
            await Navigation.PushAsync(subPage);
        }

        protected override bool OnBackButtonPressed()
        {
            // Begin an asyncronous task on the UI thread because we intend to ask the users permission.
            Device.BeginInvokeOnMainThread(async () =>
            {
                if (await DisplayAlert("Warning", "Are you sure you want to close session?", "Yes", "No"))
                {
                    client.session.Close();
                   
                    base.OnBackButtonPressed();
                    
                    await Navigation.PopToRootAsync();
                }
            });

            // Always return true because this method is not asynchronous.
            // We must handle the action ourselves: see above.
            return true;
        }

        private async void OnBrowse(object sender, EventArgs e)
        {
            ContentPage browsePage = new BrowsePage(client, client.GetRootNode());
            browsePage.Title = "OPC Browse Service";
            await Navigation.PushAsync(browsePage);
        }

        private async void onViewSubs(object sender, EventArgs e)
        {
            ContentPage subsPage = new SubscriptionsPage(client);
            subsPage.Title = "Subscriptions View";
            await Navigation.PushAsync(subsPage);
        }

        private void SessionCloseBar(object sender, EventArgs e)
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                if (await DisplayAlert("Warning", "Are you sure you want to close session?", "Yes", "No"))
                {
                    client.session.Close();

                    base.OnBackButtonPressed();

                    await Navigation.PopToRootAsync();
                }
            });
        }
    }
}