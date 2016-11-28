using System;
using System.Collections.Generic;
using System.Data.SQLite;
using SQLite.Utils;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace WebTester {

    public class Monitor {
		
		public static Monitor proxy;
		private ProxyServer proxyServer;
        private string tableName = "";
        private string dataFolderPath;
        private string database;
        private string dataSource;

		#region Event Handlers
        public async Task OnRequest(object sender, SessionEventArgs e) {
        	Console.WriteLine(e.WebSession.Request.Url);
        	var requestHeaders = e.WebSession.Request.RequestHeaders;
        	var method = e.WebSession.Request.Method.ToUpper();
        	if ((method == "POST" || method == "PUT" || method == "PATCH"))
        	{
        		byte[] bodyBytes = await e.GetRequestBody();
        		await e.SetRequestBody(bodyBytes);
        		string bodyString = await e.GetRequestBodyAsString();
        		await e.SetRequestBodyString(bodyString);
        	}
        }

        public async Task OnResponse(object sender, SessionEventArgs e) {
        	var responseHeaders = e.WebSession.Response.ResponseHeaders;
        	Console.WriteLine(String.Format("PID: {0}", e.WebSession.ProcessId.Value));
        	if (e.WebSession.Request.Method == "GET" || e.WebSession.Request.Method == "POST")
        	{
        		if (e.WebSession.Response.ResponseStatusCode == "200")
        		{
        			if (e.WebSession.Response.ContentType!=null && e.WebSession.Response.ContentType.Trim().ToLower().Contains("text/html"))
        			{
        				byte[] bodyBytes = await e.GetResponseBody();
        				await e.SetResponseBody(bodyBytes);

        				string body = await e.GetResponseBodyAsString();
        				await e.SetResponseBodyString(body);
        			}
        		}
        	}
        }

        public Task OnCertificateValidation(object sender, CertificateValidationEventArgs e) {
            if (e.SslPolicyErrors == System.Net.Security.SslPolicyErrors.None) {
                e.IsValid = true;
            }
            return Task.FromResult(0);
        }

        public Task OnCertificateSelection(object sender, CertificateSelectionEventArgs e) {
            
            return Task.FromResult(0);
        }
        #endregion
        #region DB Helpers
        bool TestConnection() {
            Console.WriteLine(String.Format("Testing database connection {0}...", database));
            try  {
                using (SQLiteConnection conn = new SQLiteConnection(dataSource)) {
                    conn.Open();
                    conn.Close();
                }
                return true;
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.ToString());
                return false;
            }
        }

        public bool insert(Dictionary<string, object> dic) {
            try {
                using (SQLiteConnection conn = new SQLiteConnection(dataSource)) {
                    using (SQLiteCommand cmd = new SQLiteCommand()) {
                        cmd.Connection = conn;
                        conn.Open();
                        SQLiteHelper sh = new SQLiteHelper(cmd);
                        sh.Insert(tableName, dic);
                        conn.Close();
                        return true;
                    }
                }
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.ToString());
                return false;
            }
        }

        public void createTable() {
            using (SQLiteConnection conn = new SQLiteConnection(dataSource)) {
                using (SQLiteCommand cmd = new SQLiteCommand()) {
                    cmd.Connection = conn;
                    conn.Open();
                    SQLiteHelper sh = new SQLiteHelper(cmd);
                    sh.DropTable(tableName);

                    SQLiteTable tb = new SQLiteTable(tableName);
                    tb.Columns.Add(new SQLiteColumn("id", true));
                    tb.Columns.Add(new SQLiteColumn("url", ColType.Text));
                    tb.Columns.Add(new SQLiteColumn("referer", ColType.Text));
                    tb.Columns.Add(new SQLiteColumn("status", ColType.Integer));
                    tb.Columns.Add(new SQLiteColumn("duration", ColType.Decimal));
                    sh.CreateTable(tb);
                    conn.Close();
                }
            }
        }
        #endregion

        public Monitor() {
            dataFolderPath = Directory.GetCurrentDirectory();
            database = String.Format("{0}\\data.db", dataFolderPath);
            dataSource = "data source=" + database;
            tableName = "product";
            proxyServer = new ProxyServer();
            #region Attach Event handlers            
            proxyServer.TrustRootCertificate = true;
            proxyServer.BeforeRequest += OnRequest;
            proxyServer.BeforeResponse += OnResponse;
            proxyServer.ServerCertificateValidationCallback += OnCertificateValidation;
            proxyServer.ClientCertificateSelectionCallback += OnCertificateSelection;
	        #endregion
        }

        public void Start() {
            Console.WriteLine("Starting Titanium.");
            dataFolderPath = Directory.GetCurrentDirectory();
            database = String.Format("{0}\\fiddler-data.db", dataFolderPath);
            dataSource = "data source=" + database;
            tableName = "product";

            TestConnection();
            createTable();
            int open_port = getAvailablePort();
			proxyServer.Start();			
            // Wait  for the user to stop
            Console.WriteLine("Hit CTRL+C to end session.");
        }

        public void Stop() {
            Console.WriteLine("Shut down Titanium proxy server.");
            proxyServer.BeforeRequest -= OnRequest;
            proxyServer.BeforeResponse -= OnResponse;
            proxyServer.ServerCertificateValidationCallback -= OnCertificateValidation;
            proxyServer.ClientCertificateSelectionCallback -= OnCertificateSelection;
            proxyServer.Stop();
            System.Threading.Thread.Sleep(1);
        }

        public static void Main(string[] args) {
            proxy = new Monitor();
            #region Attach Event handlers
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
            #endregion
            proxy.Start();
            Object forever = new Object();
            lock (forever) {
                System.Threading.Monitor.Wait(forever);
            }
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e) {
            Console.WriteLine("Stop.");
            proxy.Stop();
            System.Threading.Thread.Sleep(1);
        }
    }
}