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
new FontGeneratorWindow(Data).ShowDialog();


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
    private readonly UndertaleData utData;
    PrivateFontCollection fontCollection = new PrivateFontCollection();

    public FontGenerator(string trueTypeFontPath, string fontName, string displayName, float emSize, FontStyle fontStyle, FontVersion fontVersion, UndertaleData data)
    {
        this.fontCollection.AddFontFile(trueTypeFontPath);
        this.font = new Font(fontCollection.Families[0], emSize, fontStyle);
        this.fontName = fontName;
        this.displayName = displayName;
        this.fontVersion = fontVersion;
        this.utData = data;
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
        utFont.Name = utData.Strings.MakeString(fontName);
        utFont.DisplayName = utData.Strings.MakeString(displayName);
        utFont.ScaleX = 1;
        utFont.ScaleY = 1;
        utFont.Charset = 0;
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

        int x = 0, y = 0;
        int maxHeight = 0;
        foreach (char c in chars)
        {
            var glyph = new UndertaleModLib.Models.UndertaleFont.Glyph();
            glyph.Shift = 0;
            glyph.Offset = 0;
            glyph.Character = c;

            var charSize = gfx.MeasureString(new string(c, 1), font);
            maxHeight = Math.Max(maxHeight, Convert.ToInt32(charSize.Height));
            if (x + charSize.Width > textureBmp.Width)
            {
                x = 0;
                y += Convert.ToInt32(maxHeight);
                maxHeight = 0;
            }

            gfx.DrawString(new string(c, 1), font, whiteBrush, new Point(x, y));
            glyph.SourceX = Convert.ToUInt16(x);
            glyph.SourceY = Convert.ToUInt16(y);
            glyph.SourceWidth = Convert.ToUInt16(charSize.Width);
            glyph.SourceHeight = Convert.ToUInt16(charSize.Height);
            glyph.Shift = Convert.ToInt16(charSize.Width);
            utFont.Glyphs.Add(glyph);

            x += Convert.ToInt32(charSize.Width);
        }
        gfx.Flush();

        var textureHeight = Nlpo2(y + maxHeight);
        if (textureHeight < textureBmp.Height)
        {
            textureBmp = textureBmp.Clone(new Rectangle(0, 0, textureBmp.Width, textureHeight), textureBmp.PixelFormat);
        }

        var embedTexture = new UndertaleModLib.Models.UndertaleEmbeddedTexture();
        embedTexture.Name = utData.Strings.MakeString("Texture " + fontName);
        using (var ms = new MemoryStream())
        {
            textureBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            embedTexture.TextureData.TextureBlob = ms.ToArray();
        }

        var texturePage = new UndertaleModLib.Models.UndertaleTexturePageItem();
        texturePage.Name = utData.Strings.MakeString("PageItem " + fontName);
        texturePage.TexturePage = embedTexture;
        texturePage.SourceX = 0;
        texturePage.SourceY = 0;
        texturePage.SourceWidth = (ushort)textureBmp.Width;
        texturePage.SourceHeight = (ushort)textureBmp.Height;
        texturePage.TargetX = 0;
        texturePage.TargetY = 0;
        texturePage.TargetWidth = (ushort)textureBmp.Width;
        texturePage.TargetHeight = (ushort)textureBmp.Height;
        texturePage.BoundingWidth = (ushort)textureBmp.Width;
        texturePage.BoundingHeight = (ushort)textureBmp.Height;

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

#region FontGeneratorWindow.cs
public partial class FontGeneratorWindow : Form
{
    private FontGenerator fontGenerator;
    private UndertaleData utData;

    public FontGeneratorWindow(UndertaleData data)
    {
        this.utData = data;
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

    private void inputFilePathsChanged(object sender, EventArgs e)
    {
        if (File.Exists(tbCharsetPath.Text) && File.Exists(tbTtfPath.Text))
        {
            saveToolStripMenuItem.Enabled = true;
        }
    }

    private void RenderSample()
    {
        if (File.Exists(tbTtfPath.Text))
        {
            try
            {
                fontGenerator = CreateFontGenerator();
                pbPreview.Image = fontGenerator.RenderSampleString(tbPreviewText.Text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    private FontGenerator CreateFontGenerator()
    {
        FontStyle fontStyle = ParseFontStyle();
        FontGenerator.FontVersion fontVersion = ParseFontVersion();

        return new FontGenerator(
            tbTtfPath.Text,
            tbFontName.Text,
            tbFontDispName.Text,
            Convert.ToSingle(nudFontSize.Value),
            fontStyle,
            fontVersion,
            utData
            );
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
        if (File.Exists(tbCharsetPath.Text) && File.Exists(tbTtfPath.Text))
        {
            var charset = File.ReadAllText(tbCharsetPath.Text);
            fontGenerator = CreateFontGenerator();
            var font = fontGenerator.GenerateFont(charset.ToList());

            if (!SaveResource(font))
                return;

            SaveResource(font.Texture);
            SaveResource(font.Texture.TexturePage);
        }
    }

    private bool SaveResource(UndertaleResource res)
    {
        Type resType = res.GetType();
        string utTypeSuffix = resType.Name.Remove(0, "Undertale".Length);

        var property = utData.GetType().GetProperties().Where(x => x.PropertyType.Name == "IList`1")
                                                .FirstOrDefault(x => x.PropertyType.GetGenericArguments()[0] == resType);
        if (property is null)
            throw new MissingMemberException($"\"UndertaleData\" doesn't contain a resource list of type \"{resType.FullName}\".");

        System.Collections.IList resList = property.GetValue(utData, null) as System.Collections.IList;

        var index = resList.Cast<UndertaleResource>().ToList().FindIndex(r =>
        {
            if (r is UndertaleNamedResource namedRes)
            {
                return namedRes.Name.Content == ((UndertaleNamedResource)res).Name.Content;
            }
            else if (r is UndertaleModLib.Models.UndertaleString utString)
            {
                return utString.Content == ((UndertaleModLib.Models.UndertaleString)res).Content;
            }
            else
            {
                throw new NotSupportedException();
            }
        });
        if (index != -1)
        {
            var resName = res is UndertaleNamedResource ? ((UndertaleNamedResource)res).Name.ToString() :
             res is UndertaleString ? ((UndertaleString)res).ToString() : "resource";
            var result = MessageBox.Show($"A {utTypeSuffix} {resName} is already exists.\nDo you want to replace it?", "Warning", MessageBoxButtons.YesNo);
            if (result == DialogResult.No)
                return false;
            resList[index] = res;
        }
        else
        {
            resList.Add(res);
        }
        return true;
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

    private void FontPropertiesWindow_Load(object sender, EventArgs e)
    {
        NewFont();
    }
}
#endregion

#region FontGeneratorWindow.Designer.cs
partial class FontGeneratorWindow
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
        this.tbCharsetPath = new System.Windows.Forms.TextBox();
        this.label1 = new System.Windows.Forms.Label();
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
        this.gbFontProperties.Location = new System.Drawing.Point(12, 28);
        this.gbFontProperties.Name = "gbFontProperties";
        this.gbFontProperties.Size = new System.Drawing.Size(483, 251);
        this.gbFontProperties.TabIndex = 1;
        this.gbFontProperties.TabStop = false;
        this.gbFontProperties.Text = "Properties";
        // 
        // tbCharsetPath
        // 
        this.tbCharsetPath.Location = new System.Drawing.Point(6, 222);
        this.tbCharsetPath.MaxLength = 256;
        this.tbCharsetPath.Name = "tbCharsetPath";
        this.tbCharsetPath.Size = new System.Drawing.Size(310, 23);
        this.tbCharsetPath.TabIndex = 14;
        this.tbCharsetPath.TextChanged += new System.EventHandler(this.inputFilePathsChanged);
        // 
        // label1
        // 
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(6, 202);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(85, 17);
        this.label1.TabIndex = 13;
        this.label1.Text = "Charset path:";
        // 
        // btnChooseTtf
        // 
        this.btnChooseTtf.Location = new System.Drawing.Point(64, 28);
        this.btnChooseTtf.Name = "btnChooseTtf";
        this.btnChooseTtf.Size = new System.Drawing.Size(75, 23);
        this.btnChooseTtf.TabIndex = 12;
        this.btnChooseTtf.Text = "Choose";
        this.btnChooseTtf.UseVisualStyleBackColor = true;
        this.btnChooseTtf.Click += new System.EventHandler(this.btnChooseTtf_Click);
        // 
        // btnChooseCharset
        // 
        this.btnChooseCharset.Location = new System.Drawing.Point(97, 199);
        this.btnChooseCharset.Name = "btnChooseCharset";
        this.btnChooseCharset.Size = new System.Drawing.Size(75, 23);
        this.btnChooseCharset.TabIndex = 12;
        this.btnChooseCharset.Text = "Choose";
        this.btnChooseCharset.UseVisualStyleBackColor = true;
        this.btnChooseCharset.Click += new System.EventHandler(this.btnChooseCharset_Click);
        // 
        // cbFontVer
        // 
        this.cbFontVer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cbFontVer.FormattingEnabled = true;
        this.cbFontVer.Items.AddRange(new object[] {
            "Orign",
            "2.3 and above"});
        this.cbFontVer.Location = new System.Drawing.Point(332, 165);
        this.cbFontVer.Name = "cbFontVer";
        this.cbFontVer.Size = new System.Drawing.Size(145, 25);
        this.cbFontVer.TabIndex = 11;
        // 
        // lblFontVer
        // 
        this.lblFontVer.AutoSize = true;
        this.lblFontVer.Location = new System.Drawing.Point(332, 145);
        this.lblFontVer.Name = "lblFontVer";
        this.lblFontVer.Size = new System.Drawing.Size(82, 17);
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
        this.cbFontStyle.Location = new System.Drawing.Point(332, 109);
        this.cbFontStyle.Name = "cbFontStyle";
        this.cbFontStyle.Size = new System.Drawing.Size(145, 25);
        this.cbFontStyle.TabIndex = 9;
        this.cbFontStyle.SelectedIndexChanged += new System.EventHandler(this.fontPropertiesChanged);
        // 
        // lblFontStyle
        // 
        this.lblFontStyle.AutoSize = true;
        this.lblFontStyle.Location = new System.Drawing.Point(332, 89);
        this.lblFontStyle.Name = "lblFontStyle";
        this.lblFontStyle.Size = new System.Drawing.Size(66, 17);
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
        this.nudFontSize.Location = new System.Drawing.Point(332, 52);
        this.nudFontSize.Name = "nudFontSize";
        this.nudFontSize.Size = new System.Drawing.Size(145, 23);
        this.nudFontSize.TabIndex = 7;
        this.nudFontSize.Maximum = 1000;
        this.nudFontSize.ValueChanged += new System.EventHandler(this.fontPropertiesChanged);
        // 
        // lblFontSize
        // 
        this.lblFontSize.AutoSize = true;
        this.lblFontSize.Location = new System.Drawing.Point(332, 31);
        this.lblFontSize.Name = "lblFontSize";
        this.lblFontSize.Size = new System.Drawing.Size(62, 17);
        this.lblFontSize.TabIndex = 6;
        this.lblFontSize.Text = "Font size:";
        // 
        // tbFontDispName
        // 
        this.tbFontDispName.Location = new System.Drawing.Point(6, 165);
        this.tbFontDispName.MaxLength = 256;
        this.tbFontDispName.Name = "tbFontDispName";
        this.tbFontDispName.Size = new System.Drawing.Size(310, 23);
        this.tbFontDispName.TabIndex = 5;
        // 
        // lblFontDispName
        // 
        this.lblFontDispName.AutoSize = true;
        this.lblFontDispName.Location = new System.Drawing.Point(6, 145);
        this.lblFontDispName.Name = "lblFontDispName";
        this.lblFontDispName.Size = new System.Drawing.Size(89, 17);
        this.lblFontDispName.TabIndex = 4;
        this.lblFontDispName.Text = "Display name:";
        // 
        // tbFontName
        // 
        this.tbFontName.Location = new System.Drawing.Point(6, 109);
        this.tbFontName.MaxLength = 256;
        this.tbFontName.Name = "tbFontName";
        this.tbFontName.Size = new System.Drawing.Size(310, 23);
        this.tbFontName.TabIndex = 3;
        // 
        // lblFontName
        // 
        this.lblFontName.AutoSize = true;
        this.lblFontName.Location = new System.Drawing.Point(6, 89);
        this.lblFontName.Name = "lblFontName";
        this.lblFontName.Size = new System.Drawing.Size(72, 17);
        this.lblFontName.TabIndex = 2;
        this.lblFontName.Text = "Font name:";
        // 
        // tbTtfPath
        // 
        this.tbTtfPath.Location = new System.Drawing.Point(6, 51);
        this.tbTtfPath.Name = "tbTtfPath";
        this.tbTtfPath.Size = new System.Drawing.Size(310, 23);
        this.tbTtfPath.TabIndex = 1;
        this.tbTtfPath.TextChanged += new System.EventHandler(this.fontPropertiesChanged);
        this.tbTtfPath.TextChanged += new System.EventHandler(this.inputFilePathsChanged);
        // 
        // lblTtfPath
        // 
        this.lblTtfPath.AutoSize = true;
        this.lblTtfPath.Location = new System.Drawing.Point(6, 31);
        this.lblTtfPath.Name = "lblTtfPath";
        this.lblTtfPath.Size = new System.Drawing.Size(57, 17);
        this.lblTtfPath.TabIndex = 0;
        this.lblTtfPath.Text = "Font file:";
        // 
        // lblPreview
        // 
        this.lblPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.lblPreview.AutoSize = true;
        this.lblPreview.Location = new System.Drawing.Point(18, 291);
        this.lblPreview.Name = "lblPreview";
        this.lblPreview.Size = new System.Drawing.Size(52, 17);
        this.lblPreview.TabIndex = 2;
        this.lblPreview.Text = "Preview";
        // 
        // pbPreview
        // 
        this.pbPreview.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.pbPreview.Location = new System.Drawing.Point(12, 311);
        this.pbPreview.Name = "pbPreview";
        this.pbPreview.Size = new System.Drawing.Size(483, 147);
        this.pbPreview.TabIndex = 3;
        this.pbPreview.TabStop = false;
        // 
        // fileToolStripMenuItem
        // 
        this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem});
        this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        this.fileToolStripMenuItem.Size = new System.Drawing.Size(56, 28);
        this.fileToolStripMenuItem.Text = "File";
        // 
        // newToolStripMenuItem
        // 
        this.newToolStripMenuItem.Name = "newToolStripMenuItem";
        this.newToolStripMenuItem.Size = new System.Drawing.Size(102, 22);
        this.newToolStripMenuItem.Text = "New";
        this.newToolStripMenuItem.Click += new System.EventHandler(this.newToolStripMenuItem_Click);
        // 
        // saveToolStripMenuItem
        // 
        this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
        this.saveToolStripMenuItem.Size = new System.Drawing.Size(109, 21);
        this.saveToolStripMenuItem.Text = "Save and Close";
        this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
        this.saveToolStripMenuItem.Enabled = false;
        // 
        // menuStrip1
        // 
        this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
        this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveToolStripMenuItem});
        this.menuStrip1.Location = new System.Drawing.Point(0, 0);
        this.menuStrip1.Name = "menuStrip1";
        this.menuStrip1.Size = new System.Drawing.Size(507, 25);
        this.menuStrip1.TabIndex = 0;
        this.menuStrip1.Text = "menuStrip1";
        // 
        // tbPreviewText
        // 
        this.tbPreviewText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.tbPreviewText.Enabled = false;
        this.tbPreviewText.Location = new System.Drawing.Point(76, 288);
        this.tbPreviewText.Name = "tbPreviewText";
        this.tbPreviewText.Size = new System.Drawing.Size(413, 23);
        this.tbPreviewText.TabIndex = 4;
        this.tbPreviewText.Text = "the quick brown fox jumps over the lazy dog";
        this.tbPreviewText.TextChanged += new System.EventHandler(this.fontPropertiesChanged);
        // 
        // FontPropertiesWindow
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(507, 470);
        this.Controls.Add(this.tbPreviewText);
        this.Controls.Add(this.pbPreview);
        this.Controls.Add(this.lblPreview);
        this.Controls.Add(this.gbFontProperties);
        this.Controls.Add(this.menuStrip1);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
        this.MainMenuStrip = this.menuStrip1;
        this.Name = "FontPropertiesWindow";
        this.Text = "Font Properties";
        this.Load += new System.EventHandler(this.FontPropertiesWindow_Load);
        this.gbFontProperties.ResumeLayout(false);
        this.gbFontProperties.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.nudFontSize)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.pbPreview)).EndInit();
        this.menuStrip1.ResumeLayout(false);
        this.menuStrip1.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();

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