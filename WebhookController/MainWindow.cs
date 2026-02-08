using System;
using System.IO;
using Gtk;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Gdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Application = Gtk.Application;
using Key = Gdk.Key;
using UI = Gtk.Builder.ObjectAttribute;
using Window = Gtk.Window;

namespace WebhookController {
    class MainWindow : Window {
        [UI] private InfoBar infobar = null;
        [UI] private Label infobarTitle = null;
        [UI] private Label infobarMessage = null;
        [UI] private Image infobarIcon = null; 
        
        [UI] private Button sendButton = null;
        [UI] private Button reloadButton = null;
        [UI] private ComboBox hookComboBox = null;
        [UI] private ComboBox profileComboBox = null;
        [UI] private Entry messageEntry = null;
        [UI] private Button clearHistoryButton = null;
        [UI] private Button pollButton = null;
        
        [UI] private ListStore hookStore = null;
        [UI] private ListStore profileStore = null;
        [UI] private ListStore messageStore = null;
        [UI] private ListStore pollStore = null;
        
        [UI] private RadioButton noneRadioButton = null;
        [UI] private RadioButton presetRadioButton = null;
        [UI] private RadioButton customRadioButton = null;
        
        [UI] private Entry customNickEntry = null;
        [UI] private Entry customAvatarEntry = null;

        private readonly string configPath = System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "whookctrl");
        private readonly Dictionary<string, string> configFiles = new() {
            { "config.json", "{ \"hooks\": [], \"profiles\": [] }"}
        };

        private List<(string, string)> Webhooks = [];
        private List<(string, string)> Profiles = [];
        public MainWindow() : this(new Builder("MainWindow.glade")) {}

        private HttpClient http = new();
        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow")) {
            builder.Autoconnect(this);
            DeleteEvent += Window_DeleteEvent;
            infobar.Respond += Infobar_Close;
            if (!Directory.Exists(configPath)) Directory.CreateDirectory(configPath);
            foreach (var file in configFiles) {
                string path = System.IO.Path.Join(configPath, file.Key);
                if (!File.Exists(path)) File.WriteAllText(path, file.Value);
            }
            LoadConfig();
            reloadButton.Clicked += (_, _) => LoadConfig();
            sendButton.Clicked += SendMessage;
            messageEntry.KeyPressEvent += (o, a) => {
                Console.WriteLine(a.Event.Key.ToString());
                if (a.Event.Key is Key.ISO_Enter or Key.Key_3270_Enter or Key.KP_Enter) SendMessage(o, a);
            };
            customRadioButton.Toggled += (_, _) => {
                customNickEntry.Sensitive = customRadioButton.Active;
                customAvatarEntry.Sensitive = customRadioButton.Active;
            };
            presetRadioButton.Toggled += (_, _) => {
                profileComboBox.Sensitive = presetRadioButton.Active;
            };
            clearHistoryButton.Clicked += (_, _) => messageStore.Clear();
        }

        private async void SendMessage(object sender, EventArgs e) {
            try {
                hookComboBox.GetActiveIter(out TreeIter iter);
                profileComboBox.GetActiveIter(out TreeIter profiter);
                string url = hookStore.GetValue(iter, 1) as string;
                string name = customRadioButton.Active ? customNickEntry.Text : 
                    presetRadioButton.Active ? profileStore.GetValue(profiter, 0) as string : hookStore.GetValue(iter, 0) as string;
                string msg = messageEntry.Text;
                if (string.IsNullOrWhiteSpace(msg)) {
                    infobarIcon.Stock = "gtk-dialog-warning";
                    infobarTitle.Text = "Empty message";
                    infobarMessage.Text = "Unable to send an empty message.";
                    infobar.Show();
                    return;
                }
                messageEntry.Text = "";
                Console.WriteLine($"Sending message \"{msg}\" to {url}");
                object payload = customRadioButton.Active ? new {
                        content = msg,
                        username = customNickEntry.Text,
                        avatar_url = customAvatarEntry.Text
                    } : 
                    presetRadioButton.Active ? new {
                        content = msg,
                        username = profileStore.GetValue(profiter, 0) as string,
                        avatar_url = profileStore.GetValue(profiter, 1) as string
                    } :new {
                        content = msg,
                    };
                var resp = await http.PostAsync(url, new StringContent(JsonConvert.SerializeObject(payload), Encoding.Unicode, "application/json"));
                if (!resp.IsSuccessStatusCode) {
                    infobarIcon.Stock = "gtk-dialog-error";
                    infobarTitle.Text = "Failed to send message";
                    infobarMessage.Text = $"Server responded with code {(int)resp.StatusCode}: {await resp.Content?.ReadAsStringAsync()!}";
                    infobar.Show();
                } else {
                    iter = messageStore.Append();
                    messageStore.SetValue(iter, 0, msg);
                    messageStore.SetValue(iter, 1, name);
                }
            }
            catch (Exception exc) {
                infobarIcon.Stock = "gtk-dialog-error";
                infobarTitle.Text = exc.GetType().Name;
                infobarMessage.Text = exc.Message;
                infobar.Show();
            }
        }
        
        void LoadConfig() {
            string jeyson = File.ReadAllText(System.IO.Path.Join(configPath, "config.json"));
            var template = new {
                Hooks = Array.Empty<object>(),
                Profiles = Array.Empty<object>()
            };
            var shtik = JsonConvert.DeserializeAnonymousType(jeyson, template);
            Webhooks.Clear();
            Profiles.Clear();
            hookStore.Clear();
            if (shtik.Hooks.Length == 0) {
                infobarIcon.Stock = "gtk-dialog-warning";
                infobarTitle.Text = "No hooks defined";
                infobarMessage.Text = $"There are no webhooks in {System.IO.Path.Join(configPath, "config.json")}. At least one must be defined.";
                infobar.Show();
                sendButton.Sensitive = false;
            } else {
                foreach (var hook in shtik.Hooks) {
                    if (hook is not JObject obj || !obj.TryGetValue("name", out var name) ||
                        !obj.TryGetValue("url", out var url)) continue;
                    Webhooks.Add((name!.ToString(), url!.ToString()));
                    var iter = hookStore.Append();
                    hookStore.SetValue(iter, 0, name!.ToString());
                    hookStore.SetValue(iter, 1, url!.ToString());
                }
                hookComboBox.Active = 0;
            }
            if (shtik.Profiles.Length == 0) {
                presetRadioButton.Sensitive = false;
            } else {
                foreach (var hook in shtik.Profiles) {
                    if (hook is not JObject obj || !obj.TryGetValue("name", out var name) ||
                        !obj.TryGetValue("avatar", out var avatar)) continue;
                    Profiles.Add((name!.ToString(), avatar!.ToString()));
                    var iter = profileStore.Append();
                    profileStore.SetValue(iter, 0, name!.ToString());
                    profileStore.SetValue(iter, 1, avatar!.ToString());
                }
                presetRadioButton.Sensitive = true;
                profileComboBox.Active = 0;
            }
        }

        private void Infobar_Close(object sender, RespondArgs e) {
            infobar.Hide();
            Console.WriteLine(e.ResponseId);
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a) {
            Application.Quit();
        }
    }
}