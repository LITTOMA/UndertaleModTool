// UndertaleModTool Font Generator
// Created by LITTOMA(https://github.com/LITTOMA)

using System.Drawing.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UndertaleModLib.Util;

EnsureDataLoaded();
new FontPropertiesWindow(Data).ShowDialog();


class FontGenerator
{
    public enum FontVersion
    {
        V1,
        V2_3
    }

    private readonly Font font;
    private readonly string fontName;
    private readonly string displayName;
    private readonly FontVersion fontVersion;
    PrivateFontCollection fontCollection = new PrivateFontCollection();

    public FontGenerator(string trueTypeFontPath, string fontName, string displayName, float emSize, FontStyle fontStyle, FontVersion fontVersion)
    {
        this.fontCollection.AddFontFile(trueTypeFontPath);
        this.font = new Font(fontCollection.Families[0], emSize, fontStyle);
        this.fontName = fontName;
        this.displayName = displayName;
        this.fontVersion = fontVersion;
    }

    public Bitmap RenderSampleString(string s)
    {
        Bitmap bmp = new Bitmap(1, 1);
        Graphics gfx = Graphics.FromImage(bmp);

        var stringSize = gfx.MeasureString(s, this.font);
        bmp = new Bitmap((int)stringSize.Width, (int)stringSize.Height);
        gfx = Graphics.FromImage(bmp);
        gfx.FillRectangle(new SolidBrush(Color.Black), new Rectangle(new(0, 0), stringSize.ToSize()));
        gfx.TextRenderingHint = TextRenderingHint.AntiAlias;
        gfx.DrawString(s, font, new SolidBrush(Color.White), new PointF(0, 0));

        return bmp;
    }

    public UndertaleModLib.Models.UndertaleFont GenerateFont(IList<char> chars)
    {
        chars = chars.Distinct().OrderBy(c => c).ToList();

        var utFont = new UndertaleModLib.Models.UndertaleFont();
        utFont.Name = new UndertaleModLib.Models.UndertaleString(fontName);
        utFont.DisplayName = new UndertaleModLib.Models.UndertaleString(displayName);
        utFont.ScaleX = 1;
        utFont.ScaleY = 1;
        utFont.Charset = 1;
        utFont.Ascender = 1;
        utFont.AntiAliasing = 1;
        utFont.AscenderOffset = 1;
        utFont.Bold = (font.Style & FontStyle.Bold) == FontStyle.Bold;
        utFont.Italic = (font.Style & FontStyle.Italic) == FontStyle.Italic;
        utFont.RangeStart = chars.Min(x => Convert.ToUInt16(x));
        utFont.RangeEnd = chars.Max(x => Convert.ToUInt16(x));

        switch (fontVersion)
        {
            case FontVersion.V1:
                utFont.EmSize = Convert.ToUInt32(font.SizeInPoints);
                utFont.EmSizeIsFloat = false;
                break;
            case FontVersion.V2_3:
                Span<byte> buffer = new Span<byte>(new byte[4]);
                System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(buffer, font.SizeInPoints);
                utFont.EmSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buffer);
                utFont.EmSizeIsFloat = true;
                break;
        }

        Bitmap textureBmp = new Bitmap(1, 1);
        Graphics gfx = Graphics.FromImage(textureBmp);
        var regularFontSize = gfx.MeasureString("啊", font);
        gfx.Dispose();

        var maxArea = regularFontSize.Width * chars.Count * regularFontSize.Height;
        var width = Nlpo2(Convert.ToInt32(Math.Sqrt(maxArea)));
        textureBmp = new Bitmap(width, width);
        gfx = Graphics.FromImage(textureBmp);

        var whiteBrush = new SolidBrush(Color.White);
        var blackBrush = new SolidBrush(Color.Black);

        gfx.FillRectangle(blackBrush, new Rectangle(0, 0, textureBmp.Width, textureBmp.Height));

        int x = 0, y = 0;
        foreach (char c in chars)
        {
            var glyph = new UndertaleModLib.Models.UndertaleFont.Glyph();
            glyph.Shift = 0;
            glyph.Offset = 0;
            glyph.Character = c;

            var charSize = gfx.MeasureString(new string(c, 1), font);
            if (x + charSize.Width > textureBmp.Width)
            {
                x = 0;
                y += Convert.ToInt32(regularFontSize.Height);
            }

            gfx.DrawString(new string(c, 1), font, whiteBrush, new Point(x, y));
            glyph.SourceX = Convert.ToUInt16(x);
            glyph.SourceY = Convert.ToUInt16(y);
            glyph.SourceWidth = Convert.ToUInt16(charSize.Width);
            glyph.SourceHeight = Convert.ToUInt16(charSize.Height);
            utFont.Glyphs.Add(glyph);

            x += Convert.ToInt32(charSize.Width);
        }
        gfx.Flush();

        var embedTexture = new UndertaleModLib.Models.UndertaleEmbeddedTexture();
        embedTexture.Name = new UndertaleModLib.Models.UndertaleString("Texture " + fontName);
        using (var ms = new MemoryStream())
        {
            textureBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            embedTexture.TextureData.TextureBlob = ms.ToArray();
        }

        var texturePage = new UndertaleModLib.Models.UndertaleTexturePageItem();
        texturePage.Name = new UndertaleModLib.Models.UndertaleString("PageItem " + fontName);
        texturePage.TexturePage = embedTexture;
        texturePage.SourceX = 0;
        texturePage.SourceY = 0;
        texturePage.SourceWidth = (ushort)textureBmp.Width;
        texturePage.SourceHeight = (ushort)textureBmp.Height;
        texturePage.TargetX = 0;
        texturePage.TargetY = 0;
        texturePage.TargetWidth = (ushort)textureBmp.Width;
        texturePage.TargetHeight = (ushort)textureBmp.Height;

        utFont.Texture = texturePage;

        return utFont;
    }

    static int Nlpo2(int x)
    {
        x--;
        x |= (x >> 1);
        x |= (x >> 2);
        x |= (x >> 4);
        x |= (x >> 8);
        x |= (x >> 16);
        return (x + 1);
    }
}


#region FontPropertiesWindow.cs

public partial class FontPropertiesWindow : Form
{
    private FontGenerator fontGenerator;
    private UndertaleData Data;

    public FontPropertiesWindow(UndertaleData data)
    {
        this.Data = data;
        InitializeComponent();
    }

    private void newToolStripMenuItem_Click(object sender, EventArgs e)
    {
        NewFont();
    }

    private void saveToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SaveFont();
        this.Close();
    }

    private void btnChooseTtf_Click(object sender, EventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Filter = "TrueType Fonts|*.ttf";
        dialog.Multiselect = false;
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            tbTtfPath.Text = dialog.FileName;
        }
    }

    private void btnChooseCharset_Click(object sender, EventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Filter = "Text Files|*.txt";
        dialog.Multiselect = false;
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            tbCharsetPath.Text = dialog.FileName;
        }
    }

    private void fontPropertiesChanged(object sender, EventArgs e)
    {
        RenderSample();
    }

    private void RenderSample()
    {
        if (File.Exists(tbTtfPath.Text))
        {
            try
            {
                FontStyle fontStyle = ParseFontStyle();
                FontGenerator.FontVersion fontVersion = ParseFontVersion();

                fontGenerator = new FontGenerator(
                    tbTtfPath.Text,
                    tbFontName.Text,
                    tbFontDispName.Text,
                    Convert.ToSingle(nudFontSize.Value),
                    fontStyle,
                    fontVersion
                    );
                pbPreview.Image = fontGenerator.RenderSampleString(tbPreviewText.Text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    private FontGenerator.FontVersion ParseFontVersion()
    {
        switch (cbFontVer.SelectedItem)
        {
            case "Orign":
                return FontGenerator.FontVersion.V1;
            case "2.3 and above":
                return FontGenerator.FontVersion.V2_3;
            default:
                throw new ArgumentException($"Unknown font version: {cbFontVer.SelectedItem}");
        }
    }

    private FontStyle ParseFontStyle()
    {
        switch (cbFontStyle.SelectedItem)
        {
            case "Regular":
                return FontStyle.Regular;
            case "Bold":
                return FontStyle.Bold;
            case "Italic":
                return FontStyle.Italic;
            case "ItalicBold":
                return FontStyle.Italic | FontStyle.Bold;
            default:
                throw new ArgumentException($"Unknown font style: {cbFontStyle.SelectedText}");

        }
    }

    private void NewFont()
    {
        EnableControls();
        DefaultFontProperties();
    }

    private void SaveFont()
    {
        if (fontGenerator != null && File.Exists(tbCharsetPath.Text))
        {
            var charset = File.ReadAllText(tbCharsetPath.Text);
            var font = fontGenerator.GenerateFont(charset.ToList());

            var existingFont = Data.ByName(font.Name.Content);
            if (existingFont != null)
            {
                var result = MessageBox.Show($"A font with name {font.Name} is already exists.\nDo you want to replace it?", "Warning", MessageBoxButtons.YesNo);
                if (result == DialogResult.No)
                    return;
                Data.Fonts.Remove((UndertaleFont)existingFont);
            }

            Data.Fonts.Add(font);
            Data.TexturePageItems.Add(font.Texture);
            Data.EmbeddedTextures.Add(font.Texture.TexturePage);
            Data.Strings.Add(font.Name);
            Data.Strings.Add(font.Texture.Name);
            Data.Strings.Add(font.Texture.TexturePage.Name);
        }
    }

    private void DefaultFontProperties()
    {
        tbFontName.Text = "Font_1";
        tbFontDispName.Text = "Font";
        nudFontSize.Value = 12;
        cbFontStyle.SelectedIndex = 0;
        cbFontVer.SelectedIndex = 0;
    }

    private void EnableControls()
    {
        gbFontProperties.Enabled = true;
        tbPreviewText.Enabled = true;
    }
}

#endregion

#region FontPropertiesWindow.Designer.cs

partial class FontPropertiesWindow
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.gbFontProperties = new System.Windows.Forms.GroupBox();
        this.btnChooseTtf = new System.Windows.Forms.Button();
        this.btnChooseCharset = new System.Windows.Forms.Button();
        this.cbFontVer = new System.Windows.Forms.ComboBox();
        this.lblFontVer = new System.Windows.Forms.Label();
        this.cbFontStyle = new System.Windows.Forms.ComboBox();
        this.lblFontStyle = new System.Windows.Forms.Label();
        this.nudFontSize = new System.Windows.Forms.NumericUpDown();
        this.lblFontSize = new System.Windows.Forms.Label();
        this.tbFontDispName = new System.Windows.Forms.TextBox();
        this.lblFontDispName = new System.Windows.Forms.Label();
        this.tbFontName = new System.Windows.Forms.TextBox();
        this.lblFontName = new System.Windows.Forms.Label();
        this.tbTtfPath = new System.Windows.Forms.TextBox();
        this.lblTtfPath = new System.Windows.Forms.Label();
        this.lblPreview = new System.Windows.Forms.Label();
        this.pbPreview = new System.Windows.Forms.PictureBox();
        this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.menuStrip1 = new System.Windows.Forms.MenuStrip();
        this.tbPreviewText = new System.Windows.Forms.TextBox();
        this.tbCharsetPath = new System.Windows.Forms.TextBox();
        this.label1 = new System.Windows.Forms.Label();
        this.gbFontProperties.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudFontSize)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.pbPreview)).BeginInit();
        this.menuStrip1.SuspendLayout();
        this.SuspendLayout();
        // 
        // gbFontProperties
        // 
        this.gbFontProperties.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
        | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.gbFontProperties.Controls.Add(this.tbCharsetPath);
        this.gbFontProperties.Controls.Add(this.label1);
        this.gbFontProperties.Controls.Add(this.btnChooseTtf);
        this.gbFontProperties.Controls.Add(this.btnChooseCharset);
        this.gbFontProperties.Controls.Add(this.cbFontVer);
        this.gbFontProperties.Controls.Add(this.lblFontVer);
        this.gbFontProperties.Controls.Add(this.cbFontStyle);
        this.gbFontProperties.Controls.Add(this.lblFontStyle);
        this.gbFontProperties.Controls.Add(this.nudFontSize);
        this.gbFontProperties.Controls.Add(this.lblFontSize);
        this.gbFontProperties.Controls.Add(this.tbFontDispName);
        this.gbFontProperties.Controls.Add(this.lblFontDispName);
        this.gbFontProperties.Controls.Add(this.tbFontName);
        this.gbFontProperties.Controls.Add(this.lblFontName);
        this.gbFontProperties.Controls.Add(this.tbTtfPath);
        this.gbFontProperties.Controls.Add(this.lblTtfPath);
        this.gbFontProperties.Enabled = false;
        this.gbFontProperties.Location = new System.Drawing.Point(19, 40);
        this.gbFontProperties.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.gbFontProperties.Name = "gbFontProperties";
        this.gbFontProperties.Padding = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.gbFontProperties.Size = new System.Drawing.Size(759, 354);
        this.gbFontProperties.TabIndex = 1;
        this.gbFontProperties.TabStop = false;
        this.gbFontProperties.Text = "Properties";
        // 
        // btnChooseTtf
        // 
        this.btnChooseTtf.Location = new System.Drawing.Point(101, 40);
        this.btnChooseTtf.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.btnChooseTtf.Name = "btnChooseTtf";
        this.btnChooseTtf.Size = new System.Drawing.Size(118, 32);
        this.btnChooseTtf.TabIndex = 12;
        this.btnChooseTtf.Text = "Choose";
        this.btnChooseTtf.UseVisualStyleBackColor = true;
        this.btnChooseTtf.Click += new System.EventHandler(this.btnChooseTtf_Click);
        // 
        // cbFontVer
        // 
        this.cbFontVer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cbFontVer.FormattingEnabled = true;
        this.cbFontVer.Items.AddRange(new object[] {
            "Orign",
            "2.3 and above"});
        this.cbFontVer.Location = new System.Drawing.Point(522, 233);
        this.cbFontVer.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.cbFontVer.Name = "cbFontVer";
        this.cbFontVer.Size = new System.Drawing.Size(226, 32);
        this.cbFontVer.TabIndex = 11;
        // 
        // lblFontVer
        // 
        this.lblFontVer.AutoSize = true;
        this.lblFontVer.Location = new System.Drawing.Point(522, 205);
        this.lblFontVer.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.lblFontVer.Name = "lblFontVer";
        this.lblFontVer.Size = new System.Drawing.Size(119, 24);
        this.lblFontVer.TabIndex = 10;
        this.lblFontVer.Text = "Font version:";
        // 
        // cbFontStyle
        // 
        this.cbFontStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cbFontStyle.FormattingEnabled = true;
        this.cbFontStyle.Items.AddRange(new object[] {
            "Regular",
            "Italic",
            "Bold",
            "ItalicBold"});
        this.cbFontStyle.Location = new System.Drawing.Point(522, 154);
        this.cbFontStyle.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.cbFontStyle.Name = "cbFontStyle";
        this.cbFontStyle.Size = new System.Drawing.Size(226, 32);
        this.cbFontStyle.TabIndex = 9;
        this.cbFontStyle.SelectedIndexChanged += new System.EventHandler(this.fontPropertiesChanged);
        // 
        // lblFontStyle
        // 
        this.lblFontStyle.AutoSize = true;
        this.lblFontStyle.Location = new System.Drawing.Point(522, 126);
        this.lblFontStyle.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.lblFontStyle.Name = "lblFontStyle";
        this.lblFontStyle.Size = new System.Drawing.Size(98, 24);
        this.lblFontStyle.TabIndex = 8;
        this.lblFontStyle.Text = "Font style:";
        // 
        // nudFontSize
        // 
        this.nudFontSize.DecimalPlaces = 1;
        this.nudFontSize.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
        this.nudFontSize.Location = new System.Drawing.Point(522, 73);
        this.nudFontSize.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.nudFontSize.Name = "nudFontSize";
        this.nudFontSize.Size = new System.Drawing.Size(228, 30);
        this.nudFontSize.TabIndex = 7;
        this.nudFontSize.ValueChanged += new System.EventHandler(this.fontPropertiesChanged);
        // 
        // lblFontSize
        // 
        this.lblFontSize.AutoSize = true;
        this.lblFontSize.Location = new System.Drawing.Point(522, 44);
        this.lblFontSize.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.lblFontSize.Name = "lblFontSize";
        this.lblFontSize.Size = new System.Drawing.Size(90, 24);
        this.lblFontSize.TabIndex = 6;
        this.lblFontSize.Text = "Font size:";
        // 
        // tbFontDispName
        // 
        this.tbFontDispName.Location = new System.Drawing.Point(9, 233);
        this.tbFontDispName.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.tbFontDispName.MaxLength = 256;
        this.tbFontDispName.Name = "tbFontDispName";
        this.tbFontDispName.Size = new System.Drawing.Size(485, 30);
        this.tbFontDispName.TabIndex = 5;
        // 
        // lblFontDispName
        // 
        this.lblFontDispName.AutoSize = true;
        this.lblFontDispName.Location = new System.Drawing.Point(9, 205);
        this.lblFontDispName.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.lblFontDispName.Name = "lblFontDispName";
        this.lblFontDispName.Size = new System.Drawing.Size(131, 24);
        this.lblFontDispName.TabIndex = 4;
        this.lblFontDispName.Text = "Display name:";
        // 
        // tbFontName
        // 
        this.tbFontName.Location = new System.Drawing.Point(9, 154);
        this.tbFontName.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.tbFontName.MaxLength = 256;
        this.tbFontName.Name = "tbFontName";
        this.tbFontName.Size = new System.Drawing.Size(485, 30);
        this.tbFontName.TabIndex = 3;
        // 
        // lblFontName
        // 
        this.lblFontName.AutoSize = true;
        this.lblFontName.Location = new System.Drawing.Point(9, 126);
        this.lblFontName.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.lblFontName.Name = "lblFontName";
        this.lblFontName.Size = new System.Drawing.Size(106, 24);
        this.lblFontName.TabIndex = 2;
        this.lblFontName.Text = "Font name:";
        // 
        // tbTtfPath
        // 
        this.tbTtfPath.Location = new System.Drawing.Point(9, 72);
        this.tbTtfPath.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.tbTtfPath.Name = "tbTtfPath";
        this.tbTtfPath.Size = new System.Drawing.Size(485, 30);
        this.tbTtfPath.TabIndex = 1;
        this.tbTtfPath.TextChanged += new System.EventHandler(this.fontPropertiesChanged);
        // 
        // lblTtfPath
        // 
        this.lblTtfPath.AutoSize = true;
        this.lblTtfPath.Location = new System.Drawing.Point(9, 44);
        this.lblTtfPath.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.lblTtfPath.Name = "lblTtfPath";
        this.lblTtfPath.Size = new System.Drawing.Size(84, 24);
        this.lblTtfPath.TabIndex = 0;
        this.lblTtfPath.Text = "Font file:";
        // 
        // lblPreview
        // 
        this.lblPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.lblPreview.AutoSize = true;
        this.lblPreview.Location = new System.Drawing.Point(28, 411);
        this.lblPreview.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.lblPreview.Name = "lblPreview";
        this.lblPreview.Size = new System.Drawing.Size(76, 24);
        this.lblPreview.TabIndex = 2;
        this.lblPreview.Text = "Preview";
        // 
        // pbPreview
        // 
        this.pbPreview.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.pbPreview.Location = new System.Drawing.Point(19, 439);
        this.pbPreview.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.pbPreview.Name = "pbPreview";
        this.pbPreview.Size = new System.Drawing.Size(759, 208);
        this.pbPreview.TabIndex = 3;
        this.pbPreview.TabStop = false;
        // 
        // fileToolStripMenuItem
        // 
        this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.saveToolStripMenuItem});
        this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        this.fileToolStripMenuItem.Size = new System.Drawing.Size(56, 28);
        this.fileToolStripMenuItem.Text = "File";
        // 
        // newToolStripMenuItem
        // 
        this.newToolStripMenuItem.Name = "newToolStripMenuItem";
        this.newToolStripMenuItem.Size = new System.Drawing.Size(270, 34);
        this.newToolStripMenuItem.Text = "New";
        this.newToolStripMenuItem.Click += new System.EventHandler(this.newToolStripMenuItem_Click);
        // 
        // saveToolStripMenuItem
        // 
        this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
        this.saveToolStripMenuItem.Size = new System.Drawing.Size(270, 34);
        this.saveToolStripMenuItem.Text = "Save and Close";
        this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
        // 
        // menuStrip1
        // 
        this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
        this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveToolStripMenuItem});
        this.menuStrip1.Location = new System.Drawing.Point(0, 0);
        this.menuStrip1.Name = "menuStrip1";
        this.menuStrip1.Padding = new System.Windows.Forms.Padding(9, 3, 0, 3);
        this.menuStrip1.Size = new System.Drawing.Size(797, 34);
        this.menuStrip1.TabIndex = 0;
        this.menuStrip1.Text = "menuStrip1";
        // 
        // tbPreviewText
        // 
        this.tbPreviewText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.tbPreviewText.Enabled = false;
        this.tbPreviewText.Location = new System.Drawing.Point(119, 406);
        this.tbPreviewText.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.tbPreviewText.Name = "tbPreviewText";
        this.tbPreviewText.Size = new System.Drawing.Size(647, 30);
        this.tbPreviewText.TabIndex = 4;
        this.tbPreviewText.Text = "the quick brown fox jumps over the lazy dog";
        this.tbPreviewText.TextChanged += new System.EventHandler(this.fontPropertiesChanged);
        // 
        // tbCharsetPath
        // 
        this.tbCharsetPath.Location = new System.Drawing.Point(10, 313);
        this.tbCharsetPath.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.tbCharsetPath.MaxLength = 256;
        this.tbCharsetPath.Name = "tbCharsetPath";
        this.tbCharsetPath.Size = new System.Drawing.Size(485, 30);
        this.tbCharsetPath.TabIndex = 14;
        // 
        // label1
        // 
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(10, 285);
        this.label1.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(124, 24);
        this.label1.TabIndex = 13;
        this.label1.Text = "Charset path:";
        // 
        // btnChooseCharset
        // 
        this.btnChooseCharset.Location = new System.Drawing.Point(144, 285);
        this.btnChooseCharset.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.btnChooseCharset.Name = "btnChooseCharset";
        this.btnChooseCharset.Size = new System.Drawing.Size(118, 32);
        this.btnChooseCharset.TabIndex = 12;
        this.btnChooseCharset.Text = "Choose";
        this.btnChooseCharset.UseVisualStyleBackColor = true;
        this.btnChooseCharset.Click += new System.EventHandler(this.btnChooseCharset_Click);
        // 
        // FontPropertiesWindow
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(797, 663);
        this.Controls.Add(this.tbPreviewText);
        this.Controls.Add(this.pbPreview);
        this.Controls.Add(this.lblPreview);
        this.Controls.Add(this.gbFontProperties);
        this.Controls.Add(this.menuStrip1);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
        this.MainMenuStrip = this.menuStrip1;
        this.Margin = new System.Windows.Forms.Padding(5, 4, 5, 4);
        this.Name = "FontPropertiesWindow";
        this.Text = "Font Properties";
        this.gbFontProperties.ResumeLayout(false);
        this.gbFontProperties.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudFontSize)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.pbPreview)).EndInit();
        this.menuStrip1.ResumeLayout(false);
        this.menuStrip1.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();

        this.Load += (s, e) =>
        {
            NewFont();
        };

    }

    #endregion
    private GroupBox gbFontProperties;
    private TextBox tbFontDispName;
    private Label lblFontDispName;
    private TextBox tbFontName;
    private Label lblFontName;
    private TextBox tbTtfPath;
    private Label lblTtfPath;
    private NumericUpDown nudFontSize;
    private Label lblFontSize;
    private ComboBox cbFontStyle;
    private Label lblFontStyle;
    private ComboBox cbFontVer;
    private Label lblFontVer;
    private Label lblPreview;
    private PictureBox pbPreview;
    private Button btnChooseTtf;
    private ToolStripMenuItem fileToolStripMenuItem;
    private ToolStripMenuItem newToolStripMenuItem;
    private ToolStripMenuItem saveToolStripMenuItem;
    private MenuStrip menuStrip1;
    private TextBox tbPreviewText;
    private TextBox tbCharsetPath;
    private Button btnChooseCharset;
    private Label label1;
}

#endregion