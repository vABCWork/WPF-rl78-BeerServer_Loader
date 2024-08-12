using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BeerServer
{

    // COMポートの　コンボボックス用 
    public class ComPortNameClass
    {
        string _ComPortName;

        public string ComPortName
        {
            get { return _ComPortName; }
            set { _ComPortName = value; }
        }
    }


    /// <summary>
    /// ConfSerial.xaml の相互作用ロジック
    /// </summary>
    public partial class ConfSerial : Window
    {
        public ObservableCollection<ComPortNameClass> ComPortNames;    // 通信ポート(COM1,COM2等)のコレクション
        public static SerialPort serialPort;        // シリアルポート


        public ConfSerial()
        {

            InitializeComponent();
            ComPortNames = new ObservableCollection<ComPortNameClass>();  // 通信ポートのコレクション　インスタンス生成

            ComPortComboBox.ItemsSource = ComPortNames;       // 通信ポートコンボボックスのアイテムソース指定  

            SetComPortName();                // 通信ポート名をコンボボックスへ設定

            if (ComPortNames.Count > 0)     // 通信ポートがある場合
            {
                if (serialPort.IsOpen == true)    // new Confserial()実行時に、 既に Openしている場合
                {
                    OpenInfoTextBox.Text = "通信ポート(" + serialPort.PortName + ")は、既にオープンしています。";
                    ComPortOpenButton.Content = "Close";      // ボタン表示 Close
                }
                else
                {
                    OpenInfoTextBox.Text = "通信ポート(" + serialPort.PortName + ")は、クローズしています。";
                    ComPortOpenButton.Content = "Open";      // ボタン表示 Close
                }
            }
            else
            {
                OpenInfoTextBox.Text = "通信ポートが見つかりません。";
            }

        }


        // 通信ポート名をコンボボックスへ設定
        private void SetComPortName()
        {
            ComPortNames.Clear();           // 通信ポートのコレクション　クリア


            string[] PortList = SerialPort.GetPortNames();              // 存在するシリアルポート名が配列の要素として得られる。

            foreach (string PortName in PortList)
            {
                ComPortNames.Add(new ComPortNameClass { ComPortName = PortName }); // シリアルポート名の配列を、コレクションへコピー
            }

            if (ComPortNames.Count > 0)
            {
                ComPortComboBox.SelectedIndex = 0;   // 最初のポートを選択

                ComPortOpenButton.IsEnabled = true;  // ポートOPENボタンを「有効」にする。
            }
            else
            {
                ComPortOpenButton.IsEnabled = false;  // ポートOPENボタンを「無効」にする。
            }
        }

        // 通信ポートの検索
        private void ComPortSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SetComPortName();
        }


        // 通信ポートのオープン
        private void ComPortOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen == true)    // 既に Openしている場合
            {
                try
                {
                    serialPort.Close();

                    OpenInfoTextBox.Text = "通信ポート(" + serialPort.PortName + ")を、クローズしました。";

                    ComPortComboBox.IsEnabled = true;        // 通信条件等を選択できるようにする。
                    ComPortSearchButton.IsEnabled = true;    // 通信ポート検索ボタンを有効とする。
                    ComPortOpenButton.Content = "Open"; 　　 // ボタン表示を Closeから Openへ

                }
                catch (Exception ex)
                {
                    OpenInfoTextBox.Text = ex.Message;
                }

            }
            else                      // Close状態からOpenする場合
            {
                serialPort.PortName = ComPortComboBox.Text;    // 選択したシリアルポート

                serialPort.BaudRate = 1000000;           // ボーレート 1[Mbps]
                serialPort.Parity = Parity.None;       // パリティ無し
                serialPort.StopBits = StopBits.One;    //  1 ストップビット

                serialPort.Open();             // シリアルポートをオープンする
                serialPort.DiscardInBuffer();  // 受信バッファのクリア


                ComPortComboBox.IsEnabled = false;        // 通信条件等を選択不可にする。

                ComPortSearchButton.IsEnabled = false;    // 通信ポート検索ボタンを無効とする。

                OpenInfoTextBox.Text = "通信ポート(" + serialPort.PortName + ")を、オープンしました。";

                ComPortOpenButton.Content = "Close";      // ボタン表示を OpenからCloseへ

            }

        }
        // OKボタン
        private void ConfOKButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }


}
