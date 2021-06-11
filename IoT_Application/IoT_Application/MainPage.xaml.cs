using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace IoT_Application
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MainPage : TabbedPage
    {
        const int port = 8888;
        const int millisecondsTimeOutForConnect = 1500;
        const int millisecondsForPollingLamp = 3000;
        bool needToListenLamp = true;
        bool connected = false;
        UdpClient client;
        IPAddress lampAddress = null;

        private void updateLampStatus(int time)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    needToListenLamp = true;
                    sendCommandToLamp("GET");
                    Thread.Sleep(time / 2);
                    sendCommandToLamp("ALM_GET");
                    Thread.Sleep(time / 2);
                }
            }
            );
        }

        public MainPage()
        {
            InitializeComponent();
            connectPage.IconImageSource = ImageSource.FromResource("diplom_project.resourses.icoWifi.png");
            modesPage.IconImageSource = ImageSource.FromResource("diplom_project.resourses.icoLamp.png");
            alarmClockPage.IconImageSource = ImageSource.FromResource("diplom_project.resourses.icoAlarmClock.png");
            brightnessValueText.Text = scaleValueText.Text = speedValueText.Text = "000";
            effectSwitcher.SelectedIndexChanged += effectSwitcher_SelectedIndexChanged;
            modesPage.IsEnabled = false;
            alarmClockPage.IsEnabled = false;
        }

        private void Switcher_Toggled(object sender, ToggledEventArgs e)
        {
            Switch switcher = (sender as Switch);
            string command = "ALM_SET";
            string status = e.Value ? "ON" : "OFF";
            if (switcher == mondaySwitcher)
            {
                mondayTime.IsEnabled = switcher.IsToggled;
                command += "1 ";
            }
            else if (switcher == tuesdaySwitcher)
            {
                tuesdayTime.IsEnabled = switcher.IsToggled;
                command += "2 ";
            }
            else if (switcher == wednesdaySwitcher)
            {
                command += "3 ";
                wednesdayTime.IsEnabled = switcher.IsToggled;
            }

            else if (switcher == thursdaySwitcher)
            {
                command += "4 ";
                thursdayTime.IsEnabled = switcher.IsToggled;
            }

            else if (switcher == fridaySwitcher)
            {
                command += "5 ";
                fridayTime.IsEnabled = switcher.IsToggled;
            }

            else if (switcher == saturdaySwitcher)
            {
                command += "6 ";
                saturdayTime.IsEnabled = switcher.IsToggled;
            }

            else if (switcher == sundaySwitcher)
            {
                command += "7 ";
                sundayTime.IsEnabled = switcher.IsToggled;
            }
            command += status;
            sendCommandToLamp(command);
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            Slider slider = (sender as Slider);
            if (slider == brightnessSlider)
            {
                brightnessValueText.Text = Math.Round(slider.Value).ToString();
                needToListenLamp = false;
                sendCommandToLamp("BRI" + brightnessValueText.Text);
            }
            else if (slider == speedSlider)
            {
                speedValueText.Text = Math.Round(slider.Value).ToString();
                needToListenLamp = false;
                sendCommandToLamp("SPD" + speedValueText.Text);
            }
            else if (slider == scaleSlider)
            {
                scaleValueText.Text = Math.Round(slider.Value).ToString();
                needToListenLamp = false;
                sendCommandToLamp("SCA" + scaleValueText.Text);
            }
        }
        private void sendCommandToLamp(string command)
        {
            byte[] data = Encoding.ASCII.GetBytes(command);
            client.Send(data, data.Length, "255.255.255.255", port);
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            if (connected)
            {
                DisplayAlert("Подключение", "Соединение с лампой уже установлено.", "OK");
                return;
            }
            UdpClient udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            IPEndPoint from = null;
            Task task = new Task(
                () =>
                {
                    while (true)
                    {
                        var recvBuffer = udpClient.Receive(ref from);
                        string receivedString = Encoding.UTF8.GetString(recvBuffer);
                        try
                        {
                            if (receivedString.Substring(0, 4) == "CURR")
                            {
                                lampAddress = from.Address;
                                break;
                            }
                        }
                        catch
                        {

                        };
                    }
                });
            task.Start();
            var data = Encoding.UTF8.GetBytes("GET");
            udpClient.Send(data, data.Length, "255.255.255.255", port);
            task.Wait(millisecondsTimeOutForConnect);
            udpClient.Close();
            udpClient.Dispose();
            if (lampAddress == null)
            {
                Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                DisplayAlert("Подключение", "Соединение с лампой не установлено. \n" +
                "Проверьте подключение лампы и устройства к локальной сети и попробуйте снова.", "OK"));
            }
            else
            {
                Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                    DisplayAlert("Подключение", "Соединение с лампой установлено.\nIP-адрес: "
                    + lampAddress.ToString(), "OK"));
                connected = true;
                updateLampStatus(millisecondsForPollingLamp);
                modesPage.IsEnabled = true;
                alarmClockPage.IsEnabled = true;
                client = new UdpClient();
                client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                sendCommandToLamp("GET");
                IPEndPoint senderUDP = null;
                Task.Run(
                    () =>
                    {
                        while (true)
                        {
                            var recvBuffer = client.Receive(ref senderUDP);
                            string receivedString = Encoding.UTF8.GetString(recvBuffer);
                            if (senderUDP.Address.ToString() == lampAddress.ToString())
                                proccessingReeivedMessage(receivedString);
                        }
                    });
            }
        }

        private void proccessingReeivedMessage(string message)
        {
            string[] words = message.Split(" ".ToArray());
            if (needToListenLamp)
            {
                if (words[0] == "CURR")
                {
                    // обработка состояния свечения
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => effectSwitcher.SelectedIndex = Convert.ToInt32(words[1]));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => brightnessSlider.Value = Convert.ToInt32(words[2]));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => speedSlider.Value = Convert.ToInt32(words[3]));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => scaleSlider.Value = Convert.ToInt32(words[4]));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => switcherOfOn.IsToggled = Convert.ToInt32(words[5]) == 1);
                }
                else if (words[0] == "ALMS")
                {
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => mondaySwitcher.IsToggled = Convert.ToInt32(words[1]) == 1);
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => tuesdaySwitcher.IsToggled = Convert.ToInt32(words[2]) == 1);
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => wednesdaySwitcher.IsToggled = Convert.ToInt32(words[3]) == 1);
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => thursdaySwitcher.IsToggled = Convert.ToInt32(words[4]) == 1);
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => fridaySwitcher.IsToggled = Convert.ToInt32(words[5]) == 1);
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => saturdaySwitcher.IsToggled = Convert.ToInt32(words[6]) == 1);
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => sundaySwitcher.IsToggled = Convert.ToInt32(words[7]) == 1);
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                        mondayTime.Time = new TimeSpan(Convert.ToInt32(words[8]) / 60, Convert.ToInt32(words[8]) % 60, 0));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                        tuesdayTime.Time = new TimeSpan(Convert.ToInt32(words[9]) / 60, Convert.ToInt32(words[9]) % 60, 0));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                        wednesdayTime.Time = new TimeSpan(Convert.ToInt32(words[10]) / 60, Convert.ToInt32(words[10]) % 60, 0));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                        thursdayTime.Time = new TimeSpan(Convert.ToInt32(words[11]) / 60, Convert.ToInt32(words[11]) % 60, 0));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                        fridayTime.Time = new TimeSpan(Convert.ToInt32(words[12]) / 60, Convert.ToInt32(words[12]) % 60, 0));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                        saturdayTime.Time = new TimeSpan(Convert.ToInt32(words[13]) / 60, Convert.ToInt32(words[13]) % 60, 0));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() =>
                        sundayTime.Time = new TimeSpan(Convert.ToInt32(words[14]) / 60, Convert.ToInt32(words[14]) % 60, 0));
                    Application.Current.Dispatcher.BeginInvokeOnMainThread(() => sunriseTimePicker.SelectedIndex = (Convert.ToInt32(words[15]) - 1));
                }
            }
        }

        private void effectSwitcher_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (effectSwitcher.SelectedIndex != -1)
            {
                needToListenLamp = true;
                sendCommandToLamp("EFF" + effectSwitcher.SelectedIndex);
            }
        }

        private void switcherOfOn_Toggled(object sender, ToggledEventArgs e)
        {
            if (lampAddress != null)
            {
                if (switcherOfOn.IsToggled)
                {
                    needToListenLamp = true;
                    sendCommandToLamp("P_ON");
                }
                else
                {
                    needToListenLamp = true;
                    sendCommandToLamp("P_OFF");
                }
            }
        }

        private void timeAlarm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Time")
            {
                string command = "ALM_SET";
                TimePicker timePicker = (sender as TimePicker);

                if (timePicker == mondayTime)
                    command += "1 ";

                else if (timePicker == tuesdayTime)
                    command += "2 ";

                else if (timePicker == wednesdayTime)
                    command += "3 ";

                else if (timePicker == thursdayTime)
                    command += "4 ";

                else if (timePicker == fridayTime)
                    command += "5 ";

                else if (timePicker == saturdayTime)
                    command += "6 ";

                else if (timePicker == sundayTime)
                    command += "7 ";

                command += (timePicker.Time.Hours * 60 + timePicker.Time.Minutes).ToString();
                sendCommandToLamp(command);
            }
        }

        private void sunriseTimePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sunriseTimePicker.SelectedIndex != -1)
                sendCommandToLamp("DAWN " + (sunriseTimePicker.SelectedIndex + 1).ToString());
        }
    }
}