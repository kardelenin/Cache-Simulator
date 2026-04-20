using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace mart7
{
    public partial class Form1 : Form

    {
        // --- SİSTEM DEĞİŞKENLERİ ---
        int hitCount = 0, missCount = 0, busTraffic = 0, totalProcess = 0, globalTimer = 0;

        struct CacheSlot
        {
            public bool Valid;
            public bool Dirty;
            public int Tag;
            public int LastUsed; // LRU için sayaç
            public string Data; // Her satırın kendi adresini tutması için
        }
        
        CacheSlot[] cacheMem = new CacheSlot[4]; // 4 satırlık hafıza


        public Form1()
        {
            InitializeComponent();
        }


        private void Form1_Load(object sender, EventArgs e)

        {
            comboMapping.SelectedIndex = 0; // Direct Mapping
            comboPolicy.SelectedIndex = 0;  // Write-Through
            for (int i = 0; i < 64; i++)
            {
                Label lbl = new Label();
                lbl.Name = "lblRam_" + i; // Burası önemli: Adresi bulmamızı sağlar
                lbl.Text = i.ToString("D2");
                lbl.Size = new Size(35, 35);
                lbl.BackColor = Color.White;
                lbl.BorderStyle = BorderStyle.FixedSingle;
                lbl.TextAlign = ContentAlignment.MiddleCenter;
                lbl.Margin = new Padding(2);

                flowLayoutPanelRAM.Controls.Add(lbl);
            }

        }

        private void flowLayoutPanelRAM_Paint(object sender, PaintEventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboMapping.SelectedIndex == 1) // 1 numaralı indeks "2-Way" olsun
            {
                grpSet0.BackColor = Color.AliceBlue;
                grpSet1.BackColor = Color.OldLace;
            }
            else // 0 numaralı indeks "Direct Mapping"
            {
                grpSet0.BackColor = Color.WhiteSmoke;
                grpSet1.BackColor = Color.WhiteSmoke;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (comboMapping.SelectedIndex == -1 || comboPolicy.SelectedIndex == -1)
            {
                MessageBox.Show("Lütfen hem Mapping Style hem de Write Policy seçiniz!");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtAdres.Text))
            {
                MessageBox.Show("Lütfen önce bir RAM adresi giriniz!", "Eksik Bilgi");
                txtAdres.Focus();
                return;
            }
            if (!int.TryParse(txtAdres.Text, out int address)) return;
            // --- ADRES KONTROLÜ (0-63 ARASI) ---
            if (address < 0 || address > 63)
            {
                MessageBox.Show("Lütfen 0 ile 63 arasında geçerli bir RAM adresi giriniz!", "Hatalı Adres");
                txtAdres.Clear(); // Kutuyu temizle
                txtAdres.Focus(); // İmleci tekrar oraya odakla
                return; // Kodun devamını çalıştırma
            }
            // 1. RAM'deki eski sarı boyaları temizle (Senin oluşturduğun kutular)
            foreach (Control c in flowLayoutPanelRAM.Controls)
            {
                if (c is Label) c.BackColor = Color.White;
            }

            // 2. Yeni seçilen RAM adresini sarı yap (Görsel efekt)
            Control[] ramBox = flowLayoutPanelRAM.Controls.Find("lblRam_" + address, true);
            if (ramBox.Length > 0) ramBox[0].BackColor = Color.Yellow;

            string mode = comboMapping.SelectedItem?.ToString() ?? "Direct Mapping";
            int index = -1, tag = -1;
            globalTimer++; totalProcess++;

            if (mode == "Direct Mapping")
            {
                index = address % 4;
                tag = address / 4;
                if (cacheMem[index].Valid && cacheMem[index].Tag == tag)
                {
                    hitCount++;

                   
                    LogEkle($"HIT: Adres {address} Cache'de!");
                }
                else
                {
                    if (cacheMem[index].Valid && cacheMem[index].Dirty) busTraffic++;
                    missCount++; busTraffic++;

                    
                    cacheMem[index].Valid = true; cacheMem[index].Tag = tag; cacheMem[index].Dirty = false;
                    cacheMem[index].Data = "Mem[" + address + "]";
                    LogEkle($"MISS: Adres {address} RAM'den getirildi.");
                }
            }
            else
            { // 2-Way Set Associative
                int set = address % 2;
                int s0 = set * 2, s1 = set * 2 + 1;
                tag = address / 2;
                if (cacheMem[s0].Valid && cacheMem[s0].Tag == tag) { index = s0; hitCount++; }
                else if (cacheMem[s1].Valid && cacheMem[s1].Tag == tag) { index = s1; hitCount++; }
                else
                {
                    missCount++; busTraffic++;
                    index = (!cacheMem[s0].Valid) ? s0 : (!cacheMem[s1].Valid ? s1 :
                            (cacheMem[s0].LastUsed < cacheMem[s1].LastUsed ? s0 : s1));
                    if (cacheMem[index].Valid && cacheMem[index].Dirty) busTraffic++;
                    cacheMem[index].Valid = true; cacheMem[index].Tag = tag; cacheMem[index].Dirty = false;

                }
                cacheMem[index].LastUsed = globalTimer;
                cacheMem[index].Data = "Mem[" + address + "]";
                LogEkle($"2-Way: Adres {address} -> Set {set}");
            }
            UpdateDisplay(address.ToString());
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // --- 1. BOŞ KUTU KONTROLÜ ---
            if (string.IsNullOrWhiteSpace(txtAdres.Text) || string.IsNullOrWhiteSpace(txtVeri.Text))
            {
                MessageBox.Show("Lütfen hem RAM adresini hem de yazılacak veriyi giriniz!", "Eksik Bilgi");
                return;
            }

            if (!int.TryParse(txtAdres.Text, out int address)) return;
            if (address < 0 || address > 63) { MessageBox.Show("0-63 arası adres girin!"); return; }

            string mode = comboMapping.SelectedItem?.ToString() ?? "Direct Mapping";
            string policy = comboPolicy.SelectedItem?.ToString() ?? "Write-Through";
            globalTimer++;
            totalProcess++;

            int index = -1;
            int currentTag = (mode == "Direct Mapping") ? (address / 4) : (address / 2);

            // --- 2. MAPPING VE INDEX BULMA ---
            if (mode == "Direct Mapping")
            {
                index = address % 4;
            }
            else
            {
                int set = address % 2;
                int s0 = set * 2, s1 = set * 2 + 1;
                if (cacheMem[s0].Valid && cacheMem[s0].Tag == currentTag) index = s0;
                else if (cacheMem[s1].Valid && cacheMem[s1].Tag == currentTag) index = s1;
                else index = (!cacheMem[s0].Valid) ? s0 : (!cacheMem[s1].Valid ? s1 :
                            (cacheMem[s0].LastUsed < cacheMem[s1].LastUsed ? s0 : s1));
            }

            // --- 3. WRITE HIT / MISS AYRIMI (Yeni Düzeltme) ---
            if (cacheMem[index].Valid && cacheMem[index].Tag == currentTag)
            {
                // WRITE HIT
                hitCount++;

                
                LogEkle($"WRITE HIT: Adres {address}");
            }
            else
            {
                if (policy == "Write-Through")
                {
                    // No-Write-Allocate
                    busTraffic++; // direkt RAM'e yaz
                    missCount++;

                    LogEkle($"WRITE MISS (No-Allocate): Adres {address} -> Direkt RAM");

                    UpdateDisplay();
                    return;
                }

                // Write-Back için (Write-Allocate devam eder)
                if (cacheMem[index].Valid && cacheMem[index].Dirty) busTraffic++;
                missCount++;
                busTraffic++;

                cacheMem[index].Valid = true;
                cacheMem[index].Tag = currentTag;





                //allocatesiz
                /*
                // WRITE MISS
                if (cacheMem[index].Valid && cacheMem[index].Dirty) busTraffic++; // Kirli veriyi RAM'e boşalt
                missCount++;
                busTraffic++; // Yeni veriyi RAM'den çek
                              

                cacheMem[index].Valid = true;
                cacheMem[index].Tag = currentTag;
                LogEkle($"WRITE MISS: Adres {address}"); */
            }

            // --- 4. VERİ YAZMA VE POLİTİKA ---
            cacheMem[index].Data = txtVeri.Text;
            cacheMem[index].LastUsed = globalTimer;

            if (policy == "Write-Back")
            {
                cacheMem[index].Dirty = true;
                LogEkle($"   -> WB: Cache güncellendi (Dirty=1).");
            }
            else
            {
                busTraffic++; // Write-Through direkt RAM'e gider
                cacheMem[index].Dirty = false;
                LogEkle($"   -> WT: RAM'e yazıldı.");
            }

            UpdateDisplay();
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            lblDurum.Text = "Sistem Hazır"; // Label ismin neyse onu yaz
            lblDurum.ForeColor = Color.Black;
            UpdateDisplay(""); // İçini boş gönderiyoruz ki eski yazı kalmasın
            // Değişkenleri sıfırla
            hitCount = 0; missCount = 0; busTraffic = 0; totalProcess = 0; globalTimer = 0;
            Array.Clear(cacheMem, 0, cacheMem.Length);

            // RAM'deki boyaları temizle
            foreach (Control c in flowLayoutPanelRAM.Controls)
            {
                if (c is Label) c.BackColor = Color.White;
            }

            // Ekrandaki tüm Label'ları temizle
            lstLog.Items.Clear();
            UpdateDisplay("0"); // Görseli tazele

            LogEkle("Sistem tamamen sıfırlandı. Yeni simülasyona hazır.");
        }

        private void grpBoxistatistik_Enter(object sender, EventArgs e)
        {

        }

        private void lblTotal_Click(object sender, EventArgs e)
        {

        }

        private void label31_Click(object sender, EventArgs e)
        {

        }

        private void lblBus_Click(object sender, EventArgs e)
        {

        }

        private void lblDurum_Click(object sender, EventArgs e)
        {
          


        }

        private void lstLog_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label20_Click(object sender, EventArgs e)
        {

        }

        private void label29_Click(object sender, EventArgs e)
        {

        }


        private void UpdateDisplay(string sonAdres = "")
        {

                lblDurum.Text = comboMapping.Text + " | " + comboPolicy.Text;
                lblDurum.ForeColor = Color.Black;
           


            lblHit.Text = hitCount.ToString();
            lblMiss.Text = missCount.ToString();
            lblBus.Text = busTraffic.ToString();
            lblTotal.Text = totalProcess.ToString();
            double oran = totalProcess > 0 ? ((double)hitCount / totalProcess * 100) : 0;
            lblBasari.Text = "%" + oran.ToString("0.0");

           

            for (int i = 0; i < 4; i++)
            {
                var vBit = this.Controls.Find("lblV" + i, true).FirstOrDefault();
                var dBit = this.Controls.Find("lblD" + i, true).FirstOrDefault();
                var tLbl = this.Controls.Find("lblTag" + i, true).FirstOrDefault();
                var vLbl = this.Controls.Find("lblVeri" + i, true).FirstOrDefault();

                if (vBit != null) vBit.BackColor = cacheMem[i].Valid ? Color.LightGreen : Color.Gray;
                if (dBit != null) dBit.BackColor = cacheMem[i].Dirty ? Color.Red : Color.Gray;
                if (tLbl != null) tLbl.Text =cacheMem[i].Valid ? cacheMem[i].Tag.ToString() : "--";


                if (vLbl != null)
                {
                    if (cacheMem[i].Valid)
                    {
                        // Eğer Data boş değilse ve içinde "Mem[" geçmiyorsa (yani kullanıcı elle bir şey yazdıysa)
                        // direkt o veriyi göster, yoksa standart "Mem[X]" formatını göster.
                        vLbl.Text = cacheMem[i].Data;
                    }
                    else
                    {
                        vLbl.Text = "--";
                    }
                }

                //yazılan veriyi geri getirmeyen
                /*
                if (vLbl != null)
                {
                    // ARTIK DOĞRU VERİ BURADA: Her satır kendi 'Data'sını yazar
                    vLbl.Text = cacheMem[i].Valid ? cacheMem[i].Data : "--";
                }*/
            }
        }

        private void LogEkle(string m)
        {
            lstLog.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + " > " + m);
        }


        

    }
}
