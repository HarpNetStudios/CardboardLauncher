using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Permissions;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Steamworks;
using Steamworks.Data;

namespace CardboardLauncher
{
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisibleAttribute(true)]
    public partial class mainForm : Form
    {
        private bool drag = false; // determine if we should be moving the form
        private Point startPoint = new Point(0, 0); // also for the moving

        private bool technicalIssues = false; // Are the servers down?

        private bool success;
        private int pageSelected;

        private bool use64bit;

        private Config config;

        private string trusted_url = @"https://harpnetstudios.com";
        private string api_url = @"https://harpnetstudios.com/hnid/api/";

        private string migratePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Project Crimson Alpha");
        private string mapMigratePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Carmine Impact Alpha", "packages", "base");

        private DialogResult DisplayMessage(string text, string origin = null, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            return MessageBox.Show(text, LauncherInfo.gameName+" Launcher" + (origin != null ? " - "+origin : ""), buttons, icon);
        }

        private void ValidateSteam()
        {
            if(technicalIssues) return;
            try
            {
                SteamClient.Init(LauncherInfo.steamId, true);
                if(!SteamClient.IsLoggedOn) return;
                var ticket = SteamUser.GetAuthSessionTicket(NetIdentity.LocalHost);

                //Clipboard.SetText(BitConverter.ToString(ticket.Data).Replace("-", ""));

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(api_url+@"steam/AuthSession?ticket=" + BitConverter.ToString(ticket.Data).Replace("-", "") + "&id=" + LauncherInfo.gameId);
                request.Timeout = 15000; // hopefully will make the launcher more responsive when the servers are down
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string content = new StreamReader(response.GetResponseStream()).ReadToEnd();

                SteamLoginInfo info = JsonConvert.DeserializeObject<SteamLoginInfo>(content);
                Console.WriteLine(content);

                if (!info.valid) return; // bogus steam info
                else if (string.IsNullOrEmpty(info.gametoken)) return; // no token returned
                else
                {
                    config.gameToken = info.gametoken;
                }

            }
            catch (Exception e)
            {
                DisplayMessage("Steam login failed!\n\nException: " + e.Message);
                return;
            }
        }

        private void LoadConfig()
        {
            try
            {
                using (StreamReader r = new StreamReader("launcher.json"))
                {
                    string json = r.ReadToEnd();
                    config = JsonConvert.DeserializeObject<Config>(json);
                }
            }
            catch (FileNotFoundException)
            {
                using (StreamWriter file = File.CreateText("launcher.json"))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    //serialize object directly into file stream

                    Config tmpConf = new Config();
                    tmpConf.webUrl = "https://harpnetstudios.com/hnid/launcher/";
                    tmpConf.qConnectServ = "hnss.ga";

                    serializer.Serialize(file, tmpConf);
                    config = tmpConf;
                }
            }
        }

        private void SaveConfig()
        {
            using (StreamWriter file = File.CreateText("launcher.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                //serialize object directly into file stream
                serializer.Serialize(file, config);
            }
        }

        private void CheckVersion()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(config.webUrl + "version?id=" + LauncherInfo.gameId);
                request.Timeout = 15000; // hopefully will make the launcher more responsive when the servers are down
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string content = new StreamReader(response.GetResponseStream()).ReadToEnd();

                var ver = typeof(Program).Assembly.GetName().Version;

                if (new Version(content).CompareTo(ver) > 0) 
                {
                    DisplayMessage(string.Format("Looks like your launcher is out of date!\n\nNew version available: {0}\nYour version: {1}", content, ver.ToString()), "Launcher Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch(WebException)
            {
                technicalIssues = technicalBody.Visible = technicalTitle.Visible = true;
                //webLauncher.Visible = false;
                playOfflineChkBox.Checked = true;
            }
        }

        private bool GrabInfo(string token)
        {
            if (token=="" || technicalIssues) return false;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(api_url+@"v1/game/login?game=" + LauncherInfo.gameId);
                request.Timeout = 15000; // hopefully will make the launcher more responsive when the servers are down
                WebHeaderCollection whc = request.Headers;
                whc.Add("X-Game-Token: "+token);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string content = new StreamReader(response.GetResponseStream()).ReadToEnd();
                UserInfo info = JsonConvert.DeserializeObject<UserInfo>(content);
                Console.WriteLine(content);

                HttpStatusCode resCode = response.StatusCode;

                success = resCode == HttpStatusCode.OK && info.status == 0;

                if(success)
                {
                    config.gameToken = token;
                    userAuthLabel.Text = "User: " + info.username;
                }
                else
                {
                    config.gameToken = "";
                    userAuthLabel.Text = "Not Logged In";
                }
                playOfflineChkBox.Checked = !success;
                playButton.Enabled = success || playOfflineChkBox.Checked;
            }
            catch(WebException e)
            {
                string text = "Exception! "+e.Message;
                if(e.Status == WebExceptionStatus.ProtocolError)
                {
                    text = "Web Exception! " + e.Message+"\n";
                    text += string.Format("\nStatus Code : {0}", ((int)((HttpWebResponse)e.Response).StatusCode));
                    text += string.Format("\nStatus Description : {0}", ((HttpWebResponse)e.Response).StatusDescription);
                    text += string.Format("\nServer : {0}", ((HttpWebResponse)e.Response).Server);
                }
                DisplayMessage(text,"Account Server - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw e;
            }

            return success;
        }

        public class Config
        {
            public string webUrl = "";
            public string homeDir = "";
            public string gameToken = "";
            public string qConnectServ = "";
        }

        public class UserInfo
        {
            public string message { get; set; }
            public int status { get; set; }
            public string username { get; set; }
        }

        public class SteamLoginInfo
        {
            public string gametoken;
            public bool valid;
        }

        public mainForm()
        {
            string[] args = Environment.GetCommandLineArgs();

            InitializeComponent();

            webWarn.Location = new Point(206, 34);
            advSettings.Location = new Point(206, 34);

            advSettings.Visible = false;

            webLauncher.Location = new Point(3, 3);
            webLauncher.BringToFront();


            webLauncher.DefaultBackgroundColor = this.BackColor;

            LoadConfig();
            CheckVersion();

            if (args.Length > 1 && args[1] == "--steam") ValidateSteam();
            GrabInfo(config.gameToken);
            if(config.homeDir == "") config.homeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Carmine Impact Alpha");
            if(config.homeDir == migratePath && Directory.Exists(migratePath)) PromptMigration();
            else if(Directory.Exists(migratePath)) PromptMigration();

            if (Directory.Exists(mapMigratePath)) PromptMapMigration();

            homeDirBox.Text = config.homeDir;
            qConnectServBox.Text = config.qConnectServ;

            pageSelectCombo.SelectedIndex = 0; // HNID

            this.Text = launcherTitle.Text = LauncherInfo.gameName + " Launcher";
            playButton.Text = "&Play " + LauncherInfo.gameName;
            versionLabel.Text = "Launcher Version " + typeof(Program).Assembly.GetName().Version;
        }

        public void PromptMigration()
        {
            var migrateMessage = DisplayMessage("We've detected you're upgrading from an older version. Would you like to migrate your user data folder?", "Migration Wizard", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (migrateMessage == DialogResult.No) return;
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Carmine Impact Alpha");
                Directory.Move(migratePath, folder);
                config.homeDir = folder;
                SaveConfig();
                DisplayMessage("Successfully migrated user folder!", "Migration Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                DisplayMessage(string.Format("Migration failed! Please report this in the Discord server.\n\nError details: {0}", e.Message), "Migration Wizard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void PromptMapMigration()
        {
            var migrateMessage = DisplayMessage("We've detected you're upgrading from an older version. Would you like to migrate your custom map folder?", "Migration Wizard", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (migrateMessage == DialogResult.No) return;
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Carmine Impact Alpha", "packages", "maps");
                Directory.Move(mapMigratePath, folder);
                SaveConfig();
                DisplayMessage("Successfully migrated custom map folder!", "Migration Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                DisplayMessage(string.Format("Migration failed! Please report this in the Discord server.\n\nError details: {0}", e.Message), "Migration Wizard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
         }

        public void displayMessage(String message)
        {
            DisplayMessage(message, "Webpage");
        }
        
        public void setGameToken(string token, bool quiet=true)
        {
            if(GrabInfo(token))
            {
                if(token.Length.Equals(64))
                {
                    if(!quiet) DisplayMessage("Successfully set game token!", null, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            } else
            {
                playOfflineChkBox.Enabled = true;
            }
            DisplayMessage("Error setting game token.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public bool isTokenSet()
        {
            return config.gameToken != "";
        }

        public bool checkToken(string token)
        {
            return config.gameToken == token;
        }

        private void archGroup_Layout(object sender, LayoutEventArgs e)
        {
            if(!Environment.Is64BitOperatingSystem)
            {
                archRadio32.Checked = true;
                archGroup.Enabled = archRadio32.Enabled = archRadio64.Enabled = false;
            }
            else
            {
                archRadio64.Checked = true;
            }
        }

        private void homeDirBtn_Click(object sender, EventArgs e)
        {
            homeDirBrowser.SelectedPath = homeDirBox.Text;
            if(homeDirBrowser.ShowDialog() == DialogResult.OK)
            {
                homeDirBox.Text = homeDirBrowser.SelectedPath;
            }
        }

        private void archRadio64_CheckedChanged(object sender, EventArgs e)
        {
            use64bit = archRadio64.Checked;
        }

        private void playButton_Click(object sender, EventArgs e)
        {
            // Save config if box is checked.
            if(saveConfigChkBox.Checked) SaveConfig();

            string launchToken = config.gameToken;

            if(playOfflineChkBox.Checked)
            {
                launchToken = "OFFLINE";
                if(!technicalIssues)
                {
                    DialogResult offlineMessage = DisplayMessage("Hey, you are about to start the game in OFFLINE mode!\n\nTo experience all that the game has to offer, including multiplayer, "+(isTokenSet() ? "please disable offline mode." : "please log into your HNID account.")+"\n\nAre you sure you want to launch in offline mode?", "Offline Mode Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (offlineMessage == DialogResult.No) return;
                }
            }
            
            // Create new process definition
            ProcessStartInfo gameProcess = new ProcessStartInfo();
            gameProcess.FileName = Path.Combine("bin" + (use64bit ? "64" : ""), "cardboard_msvc.exe");
            gameProcess.Arguments = "-c" + launchToken + " -q\"" + config.homeDir + "\" -glog.txt" + (qConnectChkBox.Checked&&!playOfflineChkBox.Checked ? " -x\"connect "+config.qConnectServ+"\"" : "");
             
            
            // Attempt to start process with correct arguments
            try
            {
                Process.Start(gameProcess);
                Environment.Exit(0); // Close launcher if successful game launch
            } 
            catch(Exception ex)
            {
                DisplayMessage("Error while starting game process: "+ex.Message,"Error",MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void homeDirBox_TextChanged(object sender, EventArgs e)
        {
            config.homeDir = homeDirBox.Text;
        }

        private void versionLabel_DoubleClick(object sender, EventArgs e)
        {
            DisplayMessage("Created by Yellowberry.\n\nSpecial thanks to the rest of the HarpNet crew!");
        }

        private async void mainForm_Load(object sender, EventArgs e)
        {
            webLauncher.DefaultBackgroundColor = this.BackColor;

            await webLauncher.EnsureCoreWebView2Async();

            webLauncher.CoreWebView2.AddWebResourceRequestedFilter($"*", CoreWebView2WebResourceContext.All);
            
            webLauncher.CoreWebView2.WebResourceRequested += webLauncher_AddLauncherHeader;
            webLauncher.CoreWebView2.NewWindowRequested += webLauncher_ExternalLink;

            webLauncher.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
            webLauncher.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            webLauncher.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            webLauncher.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            webLauncher.CoreWebView2.Settings.AreHostObjectsAllowed = true;
            webLauncher.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;
            webLauncher.CoreWebView2.Settings.IsPinchZoomEnabled = false;
            webLauncher.CoreWebView2.Settings.IsReputationCheckingRequired = false;
            webLauncher.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webLauncher.CoreWebView2.Settings.IsStatusBarEnabled = false;

            #if DEBUG
                webLauncher.CoreWebView2.Settings.AreDevToolsEnabled = true;
                webLauncher.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            #else
                webLauncher.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webLauncher.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            #endif  

            webLauncher.CoreWebView2.AddHostObjectToScript("form", this);

            webLauncher.CoreWebView2.Settings.UserAgent += $" CardboardLauncher/{typeof(Program).Assembly.GetName().Version}";

            webLauncher.Source = new Uri(config.webUrl);
        }

        private void webLauncher_Navigating(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            //this.webLauncher.ScrollBarsEnabled = false;
            if(e.Uri.ToString() == "about:blank") { return; }

            Console.WriteLine(e.Uri.ToString());
            if(!e.Uri.ToString().StartsWith(trusted_url)&&e.Uri.ToString()!="about:blank")
            {
                webWarn.BackColor = System.Drawing.Color.Red;
                technicalTitle.ForeColor = this.BackColor;
            }
            else 
            {
                webWarn.BackColor = this.BackColor;
                technicalTitle.ForeColor = System.Drawing.Color.FromArgb(255,36,0);
            }
        }

        private void webLauncher_ExternalLink(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            Process.Start(e.Uri.ToString());
        }

        private void gameTokenBtn_Click(object sender, EventArgs e)
        {
            GametokenDialog gtDialog = new GametokenDialog();
            gtDialog.gameToken = config.gameToken;
            if(gtDialog.ShowDialog() == DialogResult.OK)
            {
                setGameToken(gtDialog.gameToken, false);
            }
        }

        private void qConnectChkBox_CheckedChanged(object sender, EventArgs e)
        {
            qConnectServBox.Enabled = qConnectChkBox.Checked;
        }

        private void qConnectServBox_TextChanged(object sender, EventArgs e)
        {
            config.qConnectServ = qConnectServBox.Text;
        }

        private void saveConfigChkBox_CheckedChanged(object sender, EventArgs e)
        {
            saveConfigChkBox.ForeColor = saveConfigChkBox.Checked ? System.Drawing.Color.FromArgb(255, 36, 0) : SystemColors.HighlightText;
        }

        private void pageSelectCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            int ps = pageSelectCombo.SelectedIndex;
            if(ps == pageSelected) return; // Don't flash the screen trying to change the page, it looks bad. -Y

            webWarn.Visible = false;
            advSettings.Visible = false;

            switch (ps)
            {
                case 0: // HNID
                    webWarn.Visible = true;
                    break;

                case 1: // Advanced Settings
                    advSettings.Visible = true;
                    break;

                case 2: // About
                    break;

                default: 
                    break;
            }

            pageSelected = ps;
        }

        private void closeBtn_MouseClick(object sender, MouseEventArgs e)
        {
            Application.Exit();
        }

        private void closeBtn_MouseDown(object sender, MouseEventArgs e)
        {
            closeBtn.BackgroundImage = CardboardLauncher.Properties.Resources.close_click;
        }

        private void closeBtn_MouseUp(object sender, MouseEventArgs e)
        {
            closeBtn.BackgroundImage = CardboardLauncher.Properties.Resources.close;
        }

        private void Title_MouseUp(object sender, MouseEventArgs e)
        {
            this.drag = false;
        }

        private void Title_MouseDown(object sender, MouseEventArgs e)
        {
            this.startPoint = e.Location;
            this.drag = true;
        }

        private void Title_MouseMove(object sender, MouseEventArgs e)
        {
            if(this.drag)
            { // if we should be dragging it, we need to figure out some movement
                Point p1 = new Point(e.X, e.Y);
                Point p2 = this.PointToScreen(p1);
                Point p3 = new Point(p2.X - this.startPoint.X,
                                     p2.Y - this.startPoint.Y);
                this.Location = p3;
            }
        }

        private void playOfflineChkBox_CheckedChanged(object sender, EventArgs e)
        {
            if(playOfflineChkBox.Checked && !success)
            {
                playButton.Enabled = true;
            }
            else if(!success)
            {
                playButton.Enabled = false;
            }
        }

        private void webLauncher_Loaded(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                //((WebView2)sender).ExecuteScriptAsync("document.querySelector('body').style.overflow='hidden'");
            }
        }

        private void webLauncher_AddLauncherHeader(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            e.Request.Headers.SetHeader("X-Launcher-Version", typeof(Program).Assembly.GetName().Version.ToString());
            if(!string.IsNullOrEmpty(config.gameToken))
            {
                e.Request.Headers.SetHeader("X-Game-Token", config.gameToken);
            }
        }
    }
}
