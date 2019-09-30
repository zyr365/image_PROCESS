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
            FileStream fs = new FileStream(@"C:\Users\lin\Desktop\dll\B2\Port8009_Frame242.bin", FileMode.Open);
            int fsLen = (int)fs.Length;
            byte[] heByte = new byte[fsLen];
            int r = fs.Read(heByte, 0, heByte.Length);
            fs.Close();

            sNeed = BitConverter.ToString(heByte).Replace("-", "");
            thzdata.Init(sNeed.ToString());
            thzdata.StartImageProcess(this);


        }

       
    }
}
