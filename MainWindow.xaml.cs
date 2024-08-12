using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BeerServer
{
    // クラス: スイッチの動作
    //  time: 時間
    //  SW3: ON/OFF , ON=true
    //  SW4: ON/OFF , ON=true　
    //
    public class SwStatus
    {
        public string time {  get; set; }
        public bool sw3 { get; set; }
        public bool sw4 { get; set; }
    
    }



    // クラス: 動作と継続時間
    public class FnTimeData
    {
        public string fn { get; set; }   // 反時計回り、時計回り、静止、データ異常
        public int time { get; set; }    // 動作の継続時間　[sec]
        public bool IsErr                //  データ異常の場合 IsErr=true
        {
            get
            {
                if (fn == "データ異常")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

    }



    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public static byte[] sendBuf;          // 送信バッファ   
        public static int sendByteLen;         //　送信データのバイト数

        public static byte[] rcvBuf;           // 受信バッファ
        public static int srcv_pt;             // 受信データ格納位置

        public static DateTime receiveDateTime;           // 受信完了日時
        DispatcherTimer RcvWaitTimer;                    // タイマ　受信待ち用 

        public static ushort send_msg_cnt;              // 送信数 
        public static ushort disp_msg_cnt_max;          // 送受信文の表示最大数

        public static int commlog_window_cnt;           // 通信ログ用ウィンドウの表示個数


        public static uint rcvmsg_proc_cnt;  // RcvMsgProc()の実行回数    (デバック用)
        public static byte rcvmsg_proc_flg;  // RcvMsgProc()の実行中 = 1 (デバック用)                           


        public static int switch_num;  //  SWの数 ( 2つ ）
        public static int step_num; // ステップ数 ( 60 step, 1分)　

        public static byte[] prog_step;        // ステップデータ (SW3,SW4のON/OFF情報)

        public static int cnt_stop;     
        public static int cnt_cw;
        public static int cnt_ccw;
        public static int cnt_err;

        public static ObservableCollection<SwStatus> sw_status_list;      // SWのON/OFF
        public static ObservableCollection<FnTimeData> fntime_list;       // 動作と継続時間

        public MainWindow()
        {
            InitializeComponent();

            ConfSerial.serialPort = new SerialPort();    // シリアルポートのインスタンス生成

            ConfSerial.serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);  // データ受信時のイベント処理

            sendBuf = new byte[2048];     // 送信バッファ領域  serialPortのWriteBufferSize =2048 byte(デフォルト)
            rcvBuf = new byte[4096];      // 受信バッファ領域   SerialPort.ReadBufferSize = 4096 byte (デフォルト)

            disp_msg_cnt_max = 1000;        // 送受信文の表示最大数


            RcvWaitTimer = new System.Windows.Threading.DispatcherTimer();　 // タイマーの生成(受信待ちタイマ)
            RcvWaitTimer.Tick += new EventHandler(RcvWaitTimer_Tick);        // タイマーイベント
            RcvWaitTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);          // タイマーイベント発生間隔 (受信待ち時間)

            switch_num = 2;     // SW3,SW4の2つ
            step_num = 60;      // 60step = 60秒分のSW ON/OFF情報

            prog_step = new byte[step_num];

            sw_status_list = new ObservableCollection<SwStatus>();
            this.SwStatus_DataGrid.ItemsSource = sw_status_list ;

            fntime_list = new ObservableCollection<FnTimeData>();
            this.FnTime_DataGrid.ItemsSource = fntime_list;

            ini_prog_step();          // prog_step[]の初期化
            Create_SwStatus_List();  // スイッチON/OFF情報作成 （初期化)

        }

        //
        //  　prog_step{}の初期化
        //
        private void ini_prog_step()
        {
            for (int i = 0; i < step_num; i++)
            {
                prog_step[i] = 0x00;
            }
        }






        // 送信後、1000msec以内に受信文が得られないと、受信エラー
        //  
        private void RcvWaitTimer_Tick(object sender, EventArgs e)
        {

            RcvWaitTimer.Stop();        // 受信監視タイマの停止

            StatusTextBlock.Text = "Receive time out";
        }

        private delegate void DelegateFn();

        // データ受信時のイベント処理
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (rcvmsg_proc_flg == 1)     //  RcvMsgProc()の実行中の場合、処理しない
            {
                return;
            }

            int id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Console.WriteLine("DataReceivedHandlerのスレッドID : " + id);

            int rd_num = ConfSerial.serialPort.BytesToRead;       // 受信データ数

            ConfSerial.serialPort.Read(rcvBuf, srcv_pt, rd_num);   // 受信データの読み出し

            srcv_pt = srcv_pt + rd_num;     // 次回の保存位置

            int rcv_total_byte = 0;

            if (rcvBuf[0] == 0x83)             // プログラム書き込みコマンド(0x03)のレスポンス(0x83)の場合
            {
                rcv_total_byte = 4;
            }
            else if (rcvBuf[0] == 0x84)          // プログラム読み出しコマンド(0x04)のレスポンス(0x84)の場合
            {
                rcv_total_byte = 64;
            }


            if (srcv_pt == rcv_total_byte)  // 最終データ受信済み 
            {
                RcvWaitTimer.Stop();        // 受信監視タイマー　停止

                receiveDateTime = DateTime.Now;   // 受信完了時刻を得る

                rcvmsg_proc_flg = 1;       // RcvMsgProc()の実行中

                Dispatcher.BeginInvoke(new DelegateFn(RcvMsgProc)); // Delegateを生成して、RcvMsgProcを開始   (表示は別スレッドのため)
            }

        }


        //  
        //  最終データ受信後の処理
        //   表示
        //  
        private void RcvMsgProc()
        {
            if (rcvBuf[0] == 0x84)      // プログラム読み出しコマンド(0x04)のレスポンス(0x84)の場合
            {
                store_prog_step();      // 受信したプログラムを、prog_step[]へ

                Create_SwStatus_List();  // スイッチON/OFF情報作成

                Make_Program_Data();    // プログラムデータの作成

                Disp_Program_Data();    // プログラムデータの表示 

                Make_Function_Time();   // 動作と継続時間の作成

            }

            if (CommLog.rcvframe_list != null)
            {
                CommLog.rcvmsg_disp();          // 受信データの表示       
            }

            rcvmsg_proc_cnt++;         // RcvMsgProc()の実行回数　インクリメント

            rcvmsg_proc_flg = 0;       // RcvMsgProc()の完了

        }


        //
        //  受信したプログラムのデータを、prog_step[]へ格納
        //
        // 受信データ: 
        //  rcvBuf[ ]:
        //           0:　0x84 (プログラム書き込みコマンド)
        //           1: マイコン側 コマンド受信時の CRC正常,異常フラグ (正常=0)
        //           2: ステップ 0のデータ 
        //           3: 　　　　 1
        //           4:          2
        //           5:      :
        //                   :
        //          60:  ステップ 58のデータ 
        //          61:  ステップ 59のデータ 
        //          62: CRC 上位バイト
        //          63: CRC 下位バイト

        private void store_prog_step()
        {
            for ( int i = 0 ; i < step_num; i++ )
            {
                prog_step[i] = rcvBuf[i+2];
            } 
         }


        //  送信と送信データの表示
        // sendBuf[]のデータを、sendByteLenバイト　送信する
        // 戻り値  送信成功時: true
        //         送信失敗時: false

        public bool send_disp_data()
        {
            if (ConfSerial.serialPort.IsOpen == true)
            {
                srcv_pt = 0;                   // 受信データ格納位置クリア

                ConfSerial.serialPort.Write(sendBuf, 0, sendByteLen);     // データ送信

                if (CommLog.sendframe_list != null)
                {
                    CommLog.sendmsg_disp();          // 送信データの表示
                }

                send_msg_cnt++;              // 送信数インクリメント 

                RcvWaitTimer.Start();        // 受信監視タイマー　開始

                StatusTextBlock.Text = "";
                return true;
            }

            else
            {
                StatusTextBlock.Text = "Comm port closed !";
               
                return false;
            }
        }

        //
        //  SW ステータス  リストの作成
        //  prog_step[]の情報から、 swStatus のデータを作成する。
        //
        // ・Listの構造
        //   時間[sec]   SW3(b0)          SW4(b1)
        //    0(int)　　 OFF(false bool)　OFF(false bool)
        //    1　　　　　ON(true bool)　　OFF(false bool)
        //    :          :         :
        //  59           OFF(false bool)   ON(true blue)
        //
        // ・prog_step[]のデータとSW3,SW4のON/OFFの関係 (j = ループ時の変数)
        //                j=0  j=1
        //  prog_step      SW3  SW4
        //       0x00      OFF  OFF
        //       0x01      OFF   ON
        //       0x02      ON   OFF

        public void Create_SwStatus_List()
        {
        
            sw_status_list.Clear();     // リストのクリア

            for (int i = 0; i < step_num; i++)
            {
                SwStatus swStatus = new SwStatus();

                swStatus.time = i.ToString();      // 時間 

                Byte bd = prog_step[i];  // プログラムのデータ

                if (bd == 0x00)
                {
                    swStatus.sw3 = false;
                    swStatus.sw4 = false;
                }
                else if (bd == 0x01)
                {
                    swStatus.sw3 = false;
                    swStatus.sw4 = true;
                }
                else if (bd == 0x02)
                {
                    swStatus.sw3 = true;
                    swStatus.sw4 = false;
                }
                
                sw_status_list.Add(swStatus);   // リストへ追加
            }
        }

        //
        // プログラムのダウンロード　( マイコン側へ prog_step[]を書き込み )
        //   
        private void Download_Button_Click(object sender, RoutedEventArgs e)
        {

            prog_write_cmd_set();              //プログラム書き込み用データのセット

            bool fok = send_disp_data();       // データ送信

        }

        //
        //  プログラム書き込み用コマンドとプログラムデータの送信バッファへの格納
        //
        private void prog_write_cmd_set()
        {
            UInt16 crc_cd;

            sendBuf[0] = 0x03;      // プログラム書き込みコマンド
            sendBuf[1] = 0x00;      // ダミー 0

            for (int i = 0; i < 60; i++)
            {
                sendBuf[2 + i] = prog_step[i];    // プログラムのデータを送信バッファへ
            }

            crc_cd = CRC_sendBuf_Cal(62);     // CRC計算

            sendBuf[62] = (Byte)(crc_cd >> 8); // CRCは上位バイト、下位バイトの順に送信
            sendBuf[63] = (Byte)(crc_cd & 0x00ff);

            sendByteLen = 64;                   // 送信バイト数

        }


        //
        // プログラムのアップロード　( マイコンから読み出し )
        //   
        private void Upload_Button_Click(object sender, RoutedEventArgs e)
        {
            prog_read_cmd_set();              //プログラム書き込み用データのセット

            bool fok = send_disp_data();       // データ送信
        }


        //
        //  プログラム読み出し用コマンド
        //
        private void prog_read_cmd_set()
        {
            UInt16 crc_cd;

            sendBuf[0] = 0x04;      // プログラム読み出しコマンド
            sendBuf[1] = 0x00;      // ダミー 0

            crc_cd = CRC_sendBuf_Cal(2);     // CRC計算

            sendBuf[2] = (Byte)(crc_cd >> 8); // CRCは上位バイト、下位バイトの順に送信
            sendBuf[3] = (Byte)(crc_cd & 0x00ff);

            sendByteLen = 4;                   // 送信バイト数

        }

        //
        //   クリアボタン
        //  画面上のSW3,SW4 を全てOFF
        private void Clear_Button_Click(object sender, RoutedEventArgs e)
        {
            ini_prog_step();          // prog_step[]の初期化

            Create_SwStatus_List();  // スイッチON/OFF情報作成 

            Make_Program_Data();    // プログラムデータの作成

            Disp_Program_Data();    // プログラムデータの表示

            Make_Function_Time();   // 動作と継続時間の作成

        }




        // CRCの計算 (送信バッファ用)
        // sendBuf[]内のデータのCRCコードを作成
        //
        // 入力 size:データ数
        // 
        //  CRC-16 CCITT:
        //  多項式: X^16 + X^12 + X^5 + 1　
        //  初期値: 0xffff
        //  MSBファースト
        //  非反転出力
        // 
        public static UInt16 CRC_sendBuf_Cal(UInt16 size)
        {
            UInt16 crc;

            UInt16 i;

            crc = 0xffff;

            for (i = 0; i < size; i++)
            {
                crc = (UInt16)((crc >> 8) | ((UInt16)((UInt32)crc << 8)));

                crc = (UInt16)(crc ^ sendBuf[i]);
                crc = (UInt16)(crc ^ (UInt16)((crc & 0xff) >> 4));
                crc = (UInt16)(crc ^ (UInt16)((crc << 8) << 4));
                crc = (UInt16)(crc ^ (((crc & 0xff) << 4) << 1));
            }

            return crc;

        }



        //　通信ポートの設定 ダイアログを開く
        private void Serial_Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConfSerial();
            window.Owner = this;
            window.ShowDialog();
        }

        // 通信メッセージ表示用のウィンドウを開く
        private void Comm_Log_Button_Click(object sender, RoutedEventArgs e)
        {
            if (commlog_window_cnt > 0) return;   // 既に開いている場合、リターン

            var window = new CommLog();

            window.Owner = this;   // Paraウィンドウの親は、このMainWindow

            window.Show();

            commlog_window_cnt++;     // カウンタインクリメント
        }


        // 
        // チェックボックスが変更された場合の処理
        // 発生タイミング:
        // 1)マウスでチェックボックスをON/OFFした時
        // 2)データテーブル変更時 
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Make_Program_Data();   // プログラムデータの作成

            Disp_Program_Data();    // プログラムデータの表示

            Make_Function_Time();   // 動作と継続時間の作成
        }


        //   動作と継続時間の作成
        //  秒単位のSW動作データから、動作と継続時間のリストを作成
        //
        private void Make_Function_Time()
        {
            Byte bd;
            Byte pre_status = 0;    // 1秒前の状態

            cnt_stop = 0;
            cnt_cw = 0;
            cnt_ccw = 0;
            cnt_err = 0;    

            fntime_list.Clear();    // 動作と継続時間のリストのクリア

            for (int i = 0; i < step_num; i++)  // ステップ数分　繰り返す
            {
                Byte status = prog_step[i];          // プログラムステップデータの格納

                if (status == 0x00)       // SW3=OFF,SW4=OFF の場合、
                {
                     cnt_stop++;          // 停止カウントのインクリメント
                }
                else if (status == 0x01)  // SW3=OFF,SW4=ON の場合、
                {
                     cnt_cw++;            // 時計回りカウントのインクリメント
                }
                else if (status == 0x02)  // SW3=ON,SW4=OFF の場合、
                {
                     cnt_ccw++;            // 反時計回りカウントのインクリメント
                }
                else if (status == 0x03)  // SW3=ON,SW4=ON  の場合、
                {
                    cnt_err++;           // エラーデータ カウントのインクリメント
                }

                if ( i > 0 )            　　// 先頭データ以外
                {
                    if (status != pre_status)  // 1秒前と状態が異なる場合
                    {
                        Add_FnTimeList(pre_status);
                    }

                    if (i == 59)            // 最終データ
                    {  
                        Add_FnTimeList(status);
                    }
                }


                pre_status = status;            // 今回の状態を、1つ前の状態に格納
            }
        }

        //
        //  動作と継続時間のリストを作成
        //
        private void Add_FnTimeList(Byte sta)
        {
            FnTimeData fnTimeData = new FnTimeData();

            if (sta == 0x00)    // SW3=OFF,SW4=OFF の場合
            {
                fnTimeData.fn = "静止";
                fnTimeData.time = cnt_stop;
                cnt_stop = 0;
            }
            else if (sta == 0x01)  // SW3=OFF,SW4=ON の場合
            {
                fnTimeData.fn = "時計回り";
                fnTimeData.time = cnt_cw;
                cnt_cw = 0;
            }
            else if (sta == 0x02)    // SW3=ON,SW4=OFF の場合
            {
                fnTimeData.fn = "反時計回り";
                fnTimeData.time = cnt_ccw;
                cnt_ccw = 0;
            }
            else if (sta == 0x03)    // SW3=ON,SW4=ON の場合
            {
                fnTimeData.fn = "データ異常";
                fnTimeData.time = cnt_err;
                cnt_err = 0;
            }

            fntime_list.Add(fnTimeData);  // リストへ追加


        }



        //
        //   sw_statusからプログラムデータの作成 と表示  
        //   秒単位のSW動作データ( prog_step[60] )を作成し、表示する。
        //
        //　        SW3     SW4  :
        //   time  (b0)    (b1)  作成データ
        // 0  0s    OFF     OFF  : 0x00
        // 1  1s    OFF     ON   : 0x01
        // 2  2s    ON      OFF  : 0x02
        // 3  3s    ON      ON   : 0x00 (両方 ONは禁止、データは0x00となる)
        //

        private void Make_Program_Data()
        {
            Boolean b0 = false;
            Boolean b1 = false;

            Byte bd = 0x00;

            for (int i = 0; i < step_num; i++)  // ステップ数分　繰り返す
            {
                b0 = sw_status_list[i].sw3;
                b1 = sw_status_list[i].sw4;

                if ((b0 == false) && (b1 == false)) // SW3 = OFF, SW4 = OFF
                {
                    bd = 0x00;
                }
                if ((b0 == true) && (b1 == true))   // SW3 = ON, SW4 = ON ( 禁止データ )
                {
                    bd = 0x03;
                }
                if ((b0 == true) && (b1 == false)) // SW3 = ON, SW4 = OFF
                {
                    bd = 0x02;
                }
                if ((b0 == false) && (b1 == true)) // SW3 = OFF, SW4 = ON
                {
                    bd = 0x01;
                }

                prog_step[i] = bd;          // プログラムステップデータの格納

            }
        }

        //
        //  prog_step[]データの表示
        //
        private void Disp_Program_Data()
        {
            string st = "";

            for (int i = 0; i < step_num; i++)  // ステップ数分　繰り返す
            {
                Byte bd = prog_step[i];

                st = st + "0x" + bd.ToString("x2");

                if (i < (step_num - 1))     // 最終行でない場合
                {
                    st = st + "," + "\r\n";    //  次の行にデータあり
                }
            }

            tB_BitMapPackData.Text = st;        // 表示
        }




        // カラム追加時のイベント
        //
        // データテーブルの列がBoole型の場合、チェックボックス(DataGridCheckBoxColumn)が自動的に使用されるが、
        // このチェックボックスはチェックに2クリックが必要。
        // 1クリックとしたので、カラム追加時のイベント( AutoGeneratingColumn )時に、
        //  DataGridTemplateColumn を使用して、ChekcBox (1 click)を配置している。
        //
        private void SwStatus_DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {

            if (e.PropertyName == "sw3")  //  追加するセルの名称が、"sw3"の場合
            {
                var c = new DataGridTemplateColumn()
                {
                    CellTemplate = (DataTemplate)SwStatus_DataGrid.Resources["sw3"],
                    Header = "SW3"       // SW3 反時計回り
                };
                e.Column = c;
            }

            if (e.PropertyName == "sw4")  //  追加するセルの名称が、"sw4"の場合
            {
                var c = new DataGridTemplateColumn()
                {
                    CellTemplate = (DataTemplate)SwStatus_DataGrid.Resources["sw4"],
                    Header = "SW4"       // SW4
                };
                e.Column = c;
            }

        }

        //
        //  プログラムの保存　( パソコンへ保存 ）
        //
        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            string path;
            string str_one_line;

            SaveFileDialog sfd = new SaveFileDialog();           //　SaveFileDialogクラスのインスタンスを作成 

            sfd.FileName = "sw_record.csv";                              //「ファイル名」で表示される文字列を指定する

            sfd.Title = "保存先のファイルを選択してください。";        //タイトルを設定する 

            sfd.RestoreDirectory = true;                 //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする

            if (sfd.ShowDialog() == true)            //ダイアログを表示する
            {
                path = sfd.FileName;

                try
                {
                    System.IO.StreamWriter sw = new System.IO.StreamWriter(path, false, System.Text.Encoding.Default);

                    str_one_line = tB_BitMapPackData.Text;   // プログラムの文字列
                    sw.WriteLine(str_one_line);         　　 // 1行保存

                    str_one_line = make_fntime_str();       // 動作と継続時間の文字列作成
                    sw.WriteLine(str_one_line);         　　 // 1行保存

                    sw.Close();
                }

                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        //
        // 動作と継続時間の文字列作成
        //  fntime_list[] の内容
        //
        private string make_fntime_str()
        {
            string str = "";
            string header = "";

            header = "\r\n" +  "// プログラムの動作 \r\n" + "// ( " + DateTime.Now.ToString("f") + " )" + "\r\n" +  "//" + "\r\n";

            int list_cnt = fntime_list.Count;  // Listの要素数

            for ( int i = 0; i < list_cnt; i++ )
            {
                str = str + fntime_list[i].fn + "=" + fntime_list[i].time.ToString() + "[sec]" + "\r\n";
            }
            
            return header+str;
        }


        // プログラムの読み出し　(パソコンから)
        //  ビットマップデータの読み出しと、データテーブル作成
        //処理:
        // 1) データを読み出し、prog_step[pt]へ格納。
        // 2) RGB565からデータテーブル作成して、BitMap_DataGridのデータとして表示
        //
        private void Load_Button_Click(object sender, RoutedEventArgs e)
        {

            var dialog = new OpenFileDialog();   // ダイアログのインスタンスを生成

            dialog.Filter = "CSVファイル (*.csv)|*.csv|全てのファイル (*.*)|*.*";  //  // ファイルの種類を設定

            dialog.RestoreDirectory = true;                 //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする

            if (dialog.ShowDialog() == false)     // ダイアログを表示する
            {
                return;                          // キャンセルの場合、リターン
            }

            try
            {
                StreamReader sr = new StreamReader(dialog.FileName, Encoding.GetEncoding("SHIFT_JIS"));    //  CSVファイルを読みだし

                for (int i = 0; i < step_num; i++)
                {
                    string line = sr.ReadLine();    // 1行読みだし
                    string[] fields = line.Split(',');  // カンマ(,)区切りで、分割して配列へ格納

                    Byte wb;
                    if (fields[0] == "0x02")
                    {
                        wb = 2;
                    }
                    else if (fields[0] == "0x01")
                    {
                        wb =1;
                    }
                    else
                    {
                        wb = 0;
                    }

                    prog_step[i] = wb;       // prog_step[]へ格納
                }

                Create_SwStatus_List();  // スイッチON/OFF情報作成
            }

            catch (Exception ex) when (ex is IOException || ex is IndexOutOfRangeException)
            {
                MessageBox.Show(ex.Message + "\r\n File data mismatch.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
