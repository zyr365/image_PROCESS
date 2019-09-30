using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace WindowsFormsApp19
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
          


        }
       /* public string SetLabelText
        {
            set
            {
                this.label1.Text = value;
            }
        }*/
        
        THZData thzdata = new THZData();
        private void button1_Click(object sender, EventArgs e)
        {
            string sNeed;
            FileStream fs = new FileStream(@"C:\Users\lin\Desktop\ZHEN\Port8008_Frame206.bin", FileMode.Open);
            int fsLen = (int)fs.Length;
            byte[] heByte = new byte[fsLen];
            int r = fs.Read(heByte, 0, heByte.Length);
            fs.Close();

            sNeed = BitConverter.ToString(heByte).Replace("-", "");
            thzdata.Init(sNeed.ToString());
            thzdata.StartImageProcess(this);

         


        }

        private void button2_Click(object sender, EventArgs e)
        {
            string sNeed;
            FileStream fs1 = new FileStream(@"C:\Users\lin\Desktop\ZHEN\Port8008_Frame188.bin", FileMode.Open);
           int fsLen1 = (int)fs1.Length;
           byte[] heByte1 = new byte[fsLen1];
           int r1 = fs1.Read(heByte1, 0, heByte1.Length);
           fs1.Close();

           sNeed = BitConverter.ToString(heByte1).Replace("-", "");
           thzdata.Init(sNeed.ToString());
           thzdata.StartImageProcess(this);

        }
    }
}
