using EnvContract.BLL.Interfaces;
using EnvContract.Common.Helpers;
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Guna.UI2.WinForms;

namespace EnvContract.GUI.Forms.FieldAndLab
{
    public class PrintBarcodeForm : Form
    {
        private PictureBox picBarcode;
        private Guna2Button btnPrint;
        private Label lblSampleInfo;

        public PrintBarcodeForm(string sampleId, string barcodeTxt)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = LM.Get("plan_barcode_title");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;

            lblSampleInfo = new Label
            {
                Text = string.Format(LM.Get("plan_barcode_info"), sampleId, barcodeTxt),
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(lblSampleInfo);

            picBarcode = new PictureBox
            {
                Location = new Point(20, 70),
                Size = new Size(340, 100),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            // Dùng Common Helper sinh ảnh Barcode 128
            // picBarcode.Image = BarcodeHelper.GenerateBarcodeImage(barcodeTxt, 340, 100);
            picBarcode.Image = new Bitmap(340, 100); // Mock image
            this.Controls.Add(picBarcode);

            btnPrint = new Guna2Button
            {
                Text = LM.Get("plan_barcode_btn_print"),
                Location = new Point(125, 200),
                Size = new Size(150, 45),
                BorderRadius = 8,
                FillColor = Color.FromArgb(49, 87, 44)
            };
            btnPrint.Click += (s, e) => {
                var LM_Btn = EnvContract.Common.LanguageManager.Instance;
                MessageBox.Show(LM_Btn.Get("plan_barcode_msg_success"), LM_Btn.Get("msg_info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnPrint);
        }
    }
}
