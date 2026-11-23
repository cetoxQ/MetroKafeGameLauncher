using System.Drawing;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    public class LoginForm : Form
    {
        public LoginForm()
        {
            Text = "Admin Girişi";
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(320, 210);

            var lu = Theme.Label("Kullanıcı adı", Theme.Small, Theme.Muted);
            lu.Location = new Point(20, 16);
            var user = Theme.TextBox();
            user.SetBounds(20, 36, 280, 26);

            var lp = Theme.Label("Şifre", Theme.Small, Theme.Muted);
            lp.Location = new Point(20, 72);
            var pass = Theme.TextBox();
            pass.SetBounds(20, 92, 280, 26);
            pass.UseSystemPasswordChar = true;

            var err = Theme.Label("", Theme.Small, Theme.Bad);
            err.Location = new Point(20, 124);

            var ok = Theme.Button("Giriş", true);
            ok.SetBounds(180, 152, 120, 38);
            var cancel = Theme.Button("Vazgeç");
            cancel.SetBounds(50, 152, 120, 38);
            cancel.DialogResult = DialogResult.Cancel;

            ok.Click += (s, e) =>
            {
                if (AdminAuth.Check(user.Text, pass.Text))
                {
                    DialogResult = DialogResult.OK;
                    return;
                }
                err.Text = "Kullanıcı adı veya şifre hatalı.";
                pass.SelectAll();
                pass.Focus();
            };

            AcceptButton = ok;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { lu, user, lp, pass, err, ok, cancel });
        }
    }
}
