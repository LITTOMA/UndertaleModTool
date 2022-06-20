// UndertaleModTool BmFont Importer
// Created by LITTOMA(https://github.com/LITTOMA)

using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Xml.Linq;

using UndertaleModLib.Util;
using UndertaleModLib;
using UndertaleModLib.Models;

EnsureDataLoaded();
new BmFontImporter(Data).ShowDialog();

public partial class BmFontImporter : Form
{
    private readonly UndertaleData data;
    private bool fontPathError;
    private string fontPathErrorMessage;

    public BmFontImporter(UndertaleData data)
    {
        InitializeComponent();
        this.data = data;
    }

    private void btnChooseFontPath_Click(object sender, EventArgs e)
    {
        OpenFileDialog dlg = new OpenFileDialog();
        dlg.Filter = "BMFont XML|*.xml;*.fnt";
        dlg.Multiselect = false;
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            tbFontPath.Text = dlg.FileName;
        }
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        if (!File.Exists(tbFontPath.Text))
        {
            MessageBox.Show("Font file doesn't exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrEmpty(tbFontName.Text))
        {
            MessageBox.Show("Font name is empty", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrEmpty(tbFontDispName.Text))
        {
            MessageBox.Show("Font display name is empty", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        BitmapFont font = BitmapFont.FromFile(tbFontPath.Text);
        if (font.Pages.Count != 1)
        {
            MessageBox.Show(
                "This tool only supports sigle texture page fonts.\n Try to rearrange your font texture size.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        font.Pages[0] = Path.Combine(Path.GetDirectoryName(tbFontPath.Text), font.Pages[0]);
        if (!File.Exists(font.Pages[0]))
        {
            MessageBox.Show(
                $"Font texture {font.Pages[0]} missing.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        UndertaleFont utFont = MakeUtFont(font, tbFontName.Text, tbFontDispName.Text);
        SaveResource(utFont);
        SaveResource(utFont.Texture);
        SaveResource(utFont.Texture.TexturePage);
    }

    private bool SaveResource(UndertaleResource res)
    {
        Type resType = res.GetType();
        string utTypeSuffix = resType.Name.Remove(0, "Undertale".Length);

        var property = data.GetType().GetProperties().Where(x => x.PropertyType.Name == "IList`1")
                                                .FirstOrDefault(x => x.PropertyType.GetGenericArguments()[0] == resType);
        if (property is null)
            throw new MissingMemberException($"\"UndertaleData\" doesn't contain a resource list of type \"{resType.FullName}\".");

        System.Collections.IList resList = property.GetValue(data, null) as System.Collections.IList;

        var index = resList.Cast<UndertaleResource>().ToList().FindIndex(r =>
        {
            if (r is UndertaleNamedResource namedRes)
            {
                return namedRes.Name.Content == ((UndertaleNamedResource)res).Name.Content;
            }
            else if (r is UndertaleString utString)
            {
                return utString.Content == ((UndertaleString)res).Content;
            }
            else
            {
                throw new NotSupportedException();
            }
        });
        if (index != -1)
        {
            var resName = res is UndertaleNamedResource ? ((UndertaleNamedResource)res).Name.ToString() :
                res is UndertaleString utString ? utString.ToString() : "";
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

    private UndertaleFont MakeUtFont(BitmapFont font, string fontName, string displayName)
    {
        UndertaleFont utFont = new UndertaleFont();
        utFont.Name = data.Strings.MakeString(fontName);
        utFont.DisplayName = data.Strings.MakeString(displayName);
        utFont.ScaleX = 1;
        utFont.ScaleY = 1;
        utFont.Charset = 0;
        utFont.Ascender = 1;
        utFont.AntiAliasing = 1;
        utFont.AscenderOffset = 1;
        utFont.Bold = font.Info.Bold;
        utFont.Italic = font.Info.Italic;
        utFont.EmSize = (ushort)font.Info.Size;
        utFont.RangeStart = (ushort)font.Characters.Keys.Min();
        utFont.RangeEnd = (uint)font.Characters.Keys.Max();

        foreach (var character in font.Characters.Keys.OrderBy(x => x))
        {
            var c = font.Characters[character];
            UndertaleFont.Glyph glyph = new UndertaleFont.Glyph();
            glyph.Character = (ushort)character;
            glyph.SourceX = (ushort)c.X;
            glyph.SourceY = (ushort)c.Y;
            glyph.SourceWidth = (ushort)c.Width;
            glyph.SourceHeight = (ushort)c.Height;
            glyph.Shift = (short)c.XAdvance;
            glyph.Offset = (short)c.XOffset;
            utFont.Glyphs.Add(glyph);
        }

        UndertaleEmbeddedTexture texture = new UndertaleEmbeddedTexture();
        texture.GeneratedMips = 0;
        texture.Scaled = 1;
        var textureName = $"Texture {data.EmbeddedTextures.Count}";
        texture.Name = data.Strings.MakeString(textureName);
        var textureBmp = Image.FromFile(font.Pages[0]);
        using (var ms = new MemoryStream())
        {
            textureBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            texture.TextureData.TextureBlob = ms.ToArray();
        }

        UndertaleTexturePageItem texturePage = new UndertaleTexturePageItem();
        var texturePageName = $"PageItem {data.TexturePageItems.Count}";
        texturePage.Name = data.Strings.MakeString(texturePageName);
        texturePage.TexturePage = texture;
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
}

#region UI

partial class BmFontImporter
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
        this.gbSettings = new System.Windows.Forms.GroupBox();
        this.tbFontDispName = new System.Windows.Forms.TextBox();
        this.lblFontDispName = new System.Windows.Forms.Label();
        this.tbFontName = new System.Windows.Forms.TextBox();
        this.lblFontName = new System.Windows.Forms.Label();
        this.tbFontPath = new System.Windows.Forms.TextBox();
        this.btnChooseFontPath = new System.Windows.Forms.Button();
        this.lblFontPath = new System.Windows.Forms.Label();
        this.btnSave = new System.Windows.Forms.Button();
        this.gbSettings.SuspendLayout();
        this.SuspendLayout();
        // 
        // gbSettings
        // 
        this.gbSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
        | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.gbSettings.Controls.Add(this.tbFontDispName);
        this.gbSettings.Controls.Add(this.lblFontDispName);
        this.gbSettings.Controls.Add(this.tbFontName);
        this.gbSettings.Controls.Add(this.lblFontName);
        this.gbSettings.Controls.Add(this.tbFontPath);
        this.gbSettings.Controls.Add(this.btnChooseFontPath);
        this.gbSettings.Controls.Add(this.lblFontPath);
        this.gbSettings.Location = new System.Drawing.Point(12, 12);
        this.gbSettings.Name = "gbSettings";
        this.gbSettings.Size = new System.Drawing.Size(246, 280);
        this.gbSettings.TabIndex = 0;
        this.gbSettings.TabStop = false;
        this.gbSettings.Text = "Settings";
        // 
        // tbFontDispName
        // 
        this.tbFontDispName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.tbFontDispName.Location = new System.Drawing.Point(6, 229);
        this.tbFontDispName.Name = "tbFontDispName";
        this.tbFontDispName.Size = new System.Drawing.Size(234, 23);
        this.tbFontDispName.TabIndex = 6;
        // 
        // lblFontDispName
        // 
        this.lblFontDispName.AutoSize = true;
        this.lblFontDispName.Location = new System.Drawing.Point(6, 209);
        this.lblFontDispName.Name = "lblFontDispName";
        this.lblFontDispName.Size = new System.Drawing.Size(117, 17);
        this.lblFontDispName.TabIndex = 5;
        this.lblFontDispName.Text = "Font display name:";
        // 
        // tbFontName
        // 
        this.tbFontName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.tbFontName.Location = new System.Drawing.Point(6, 164);
        this.tbFontName.Name = "tbFontName";
        this.tbFontName.Size = new System.Drawing.Size(234, 23);
        this.tbFontName.TabIndex = 4;
        // 
        // lblFontName
        // 
        this.lblFontName.AutoSize = true;
        this.lblFontName.Location = new System.Drawing.Point(6, 144);
        this.lblFontName.Name = "lblFontName";
        this.lblFontName.Size = new System.Drawing.Size(72, 17);
        this.lblFontName.TabIndex = 3;
        this.lblFontName.Text = "Font name:";
        // 
        // tbFontPath
        // 
        this.tbFontPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.tbFontPath.Location = new System.Drawing.Point(6, 53);
        this.tbFontPath.Multiline = true;
        this.tbFontPath.Name = "tbFontPath";
        this.tbFontPath.Size = new System.Drawing.Size(234, 68);
        this.tbFontPath.TabIndex = 2;
        // 
        // btnChooseFontPath
        // 
        this.btnChooseFontPath.Location = new System.Drawing.Point(78, 27);
        this.btnChooseFontPath.Name = "btnChooseFontPath";
        this.btnChooseFontPath.Size = new System.Drawing.Size(75, 23);
        this.btnChooseFontPath.TabIndex = 1;
        this.btnChooseFontPath.Text = "Choose";
        this.btnChooseFontPath.UseVisualStyleBackColor = true;
        this.btnChooseFontPath.Click += new System.EventHandler(this.btnChooseFontPath_Click);
        // 
        // lblFontPath
        // 
        this.lblFontPath.AutoSize = true;
        this.lblFontPath.Location = new System.Drawing.Point(6, 30);
        this.lblFontPath.Name = "lblFontPath";
        this.lblFontPath.Size = new System.Drawing.Size(66, 17);
        this.lblFontPath.TabIndex = 0;
        this.lblFontPath.Text = "Font path:";
        // 
        // btnSave
        // 
        this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.btnSave.Location = new System.Drawing.Point(183, 298);
        this.btnSave.Name = "btnSave";
        this.btnSave.Size = new System.Drawing.Size(75, 23);
        this.btnSave.TabIndex = 1;
        this.btnSave.Text = "Save";
        this.btnSave.UseVisualStyleBackColor = true;
        this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
        // 
        // BmFontImporter
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(270, 333);
        this.Controls.Add(this.btnSave);
        this.Controls.Add(this.gbSettings);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
        this.Name = "BmFontImporter";
        this.Text = "BmFont Importer";
        this.gbSettings.ResumeLayout(false);
        this.gbSettings.PerformLayout();
        this.ResumeLayout(false);

    }

    #endregion

    private GroupBox gbSettings;
    private TextBox tbFontPath;
    private Button btnChooseFontPath;
    private Label lblFontPath;
    private Button btnSave;
    private TextBox tbFontDispName;
    private Label lblFontDispName;
    private TextBox tbFontName;
    private Label lblFontName;
}
#endregion

#region SharpFNT

// From: https://github.com/AuroraBertaOldham/SharpFNT
// Removed writing methods.
// File generated by dotnet-combine at 2022-06-20__13_47_15

//**************************************************************************************************
// Copyright (c) 2018-2020 Aurora Berta-Oldham                                                     *
// This code is made available under the MIT License.                                              *
//**************************************************************************************************



// BitmapFont.cs
public sealed class BitmapFont
{
    public const int ImplementedVersion = 3;

    internal const byte MagicOne = 66;
    internal const byte MagicTwo = 77;
    internal const byte MagicThree = 70;

    public BitmapFontInfo Info { get; set; }

    public BitmapFontCommon Common { get; set; }

    public IDictionary<int, string> Pages { get; set; }

    public IDictionary<int, Character> Characters { get; set; }

    public IDictionary<KerningPair, int> KerningPairs { get; set; }

    public int GetKerningAmount(char left, char right)
    {
        if (KerningPairs == null)
        {
            return 0;
        }

        KerningPairs.TryGetValue(new KerningPair(left, right), out var kerningValue);

        return kerningValue;
    }
    public Character GetCharacter(char character, bool tryInvalid = true)
    {
        if (Characters == null)
        {
            return null;
        }

        if (Characters.TryGetValue(character, out var result))
        {
            return result;
        }

        if (tryInvalid && Characters.TryGetValue(-1, out result))
        {
            return result;
        }

        return null;
    }

    public static BitmapFont ReadBinary(BinaryReader binaryReader)
    {
        var bitmapFont = new BitmapFont();

        var magicOne = binaryReader.ReadByte();
        var magicTwo = binaryReader.ReadByte();
        var magicThree = binaryReader.ReadByte();

        if (magicOne != MagicOne || magicTwo != MagicTwo || magicThree != MagicThree)
        {
            throw new InvalidDataException("File is not an FNT bitmap font or it is not in the binary format.");
        }

        if (binaryReader.ReadByte() != ImplementedVersion)
        {
            throw new InvalidDataException("The version specified is different from the implemented version.");
        }

        var pageCount = 0;

        while (binaryReader.PeekChar() != -1)
        {
            var blockID = (BlockID)binaryReader.ReadByte();

            switch (blockID)
            {
                case BlockID.Info:
                    {
                        bitmapFont.Info = BitmapFontInfo.ReadBinary(binaryReader);
                        break;
                    }
                case BlockID.Common:
                    {
                        bitmapFont.Common = BitmapFontCommon.ReadBinary(binaryReader, out pageCount);
                        break;
                    }
                case BlockID.Pages:
                    {
                        binaryReader.ReadInt32();

                        bitmapFont.Pages = new Dictionary<int, string>(pageCount);

                        for (var i = 0; i < pageCount; i++)
                        {
                            bitmapFont.Pages[i] = UtilityExtensions.ReadNullTerminatedString(binaryReader);
                        }

                        break;
                    }
                case BlockID.Characters:
                    {
                        var characterBlockSize = binaryReader.ReadInt32();

                        if (characterBlockSize % Character.SizeInBytes != 0)
                        {
                            throw new InvalidDataException("Invalid character block size.");
                        }

                        var characterCount = characterBlockSize / Character.SizeInBytes;

                        bitmapFont.Characters = new Dictionary<int, Character>(characterCount);

                        for (var i = 0; i < characterCount; i++)
                        {
                            var character = Character.ReadBinary(binaryReader, out var id);
                            bitmapFont.Characters[id] = character;
                        }

                        break;
                    }
                case BlockID.KerningPairs:
                    {
                        var kerningBlockSize = binaryReader.ReadInt32();

                        if (kerningBlockSize % KerningPair.SizeInBytes != 0)
                        {
                            throw new InvalidDataException("Invalid kerning block size.");
                        }

                        var kerningCount = kerningBlockSize / KerningPair.SizeInBytes;

                        bitmapFont.KerningPairs = new Dictionary<KerningPair, int>(kerningCount);

                        for (var i = 0; i < kerningCount; i++)
                        {
                            var kerningPair = KerningPair.ReadBinary(binaryReader, out var amount);
                            if (bitmapFont.KerningPairs.ContainsKey(kerningPair)) continue;
                            bitmapFont.KerningPairs[kerningPair] = amount;
                        }

                        break;
                    }
                default:
                    {
                        throw new InvalidDataException("Invalid block ID.");
                    }
            }
        }

        return bitmapFont;
    }
    public static BitmapFont ReadXML(TextReader textReader)
    {
        var bitmapFont = new BitmapFont();

        var document = XDocument.Load(textReader);

        var fontElement = document.Element("font");

        if (fontElement == null)
        {
            throw new InvalidDataException("Missing root font element in XML file.");
        }

        // Info

        var infoElement = fontElement.Element("info");
        if (infoElement != null)
        {
            bitmapFont.Info = BitmapFontInfo.ReadXML(infoElement);
        }

        // Common

        var pages = 0;

        var commonElement = fontElement.Element("common");
        if (commonElement != null)
        {
            bitmapFont.Common = BitmapFontCommon.ReadXML(commonElement, out pages);
        }

        // Pages

        var pagesElement = fontElement.Element("pages");
        if (pagesElement != null)
        {
            bitmapFont.Pages = new Dictionary<int, string>(pages);

            foreach (var pageElement in pagesElement.Elements("page"))
            {
                var id = (int?)pageElement.Attribute("id") ?? 0;
                var name = (string)pageElement.Attribute("file");
                bitmapFont.Pages[id] = name;
            }
        }

        // Characters

        var charactersElement = fontElement.Element("chars");
        if (charactersElement != null)
        {
            var count = (int?)charactersElement.Attribute("count") ?? 0;

            bitmapFont.Characters = new Dictionary<int, Character>(count);

            foreach (var characterElement in charactersElement.Elements("char"))
            {
                var character = Character.ReadXML(characterElement, out var id);
                bitmapFont.Characters[id] = character;
            }
        }

        // Kernings

        var kerningsElement = fontElement.Element("kernings");
        if (kerningsElement != null)
        {
            var count = (int?)kerningsElement.Attribute("count") ?? 0;

            bitmapFont.KerningPairs = new Dictionary<KerningPair, int>(count);

            foreach (var kerningElement in kerningsElement.Elements("kerning"))
            {
                var kerningPair = KerningPair.ReadXML(kerningElement, out var amount);
                if (bitmapFont.KerningPairs.ContainsKey(kerningPair)) continue;
                bitmapFont.KerningPairs[kerningPair] = amount;
            }
        }

        return bitmapFont;
    }
    public static BitmapFont ReadText(TextReader textReader)
    {
        var bitmapFont = new BitmapFont();

        while (textReader.Peek() != -1)
        {
            var lineSegments = TextFormatUtility.GetSegments(textReader.ReadLine());

            switch (lineSegments[0])
            {
                case "info":
                    {
                        bitmapFont.Info = BitmapFontInfo.ReadText(lineSegments);
                        break;
                    }
                case "common":
                    {
                        bitmapFont.Common = BitmapFontCommon.ReadText(lineSegments, out var pageCount);
                        bitmapFont.Pages = new Dictionary<int, string>(pageCount);
                        break;
                    }
                case "page":
                    {
                        bitmapFont.Pages = bitmapFont.Pages ?? new Dictionary<int, string>();
                        var id = TextFormatUtility.ReadInt("id", lineSegments);
                        var file = TextFormatUtility.ReadString("file", lineSegments);
                        bitmapFont.Pages[id] = file;
                        break;
                    }
                case "chars":
                    {
                        var count = TextFormatUtility.ReadInt("count", lineSegments);

                        bitmapFont.Characters = new Dictionary<int, Character>(count);

                        for (var i = 0; i < count; i++)
                        {
                            var characterLineSegments = TextFormatUtility.GetSegments(textReader.ReadLine());
                            var character = Character.ReadText(characterLineSegments, out var id);
                            bitmapFont.Characters[id] = character;
                        }

                        break;
                    }
                case "kernings":
                    {
                        var count = TextFormatUtility.ReadInt("count", lineSegments);

                        bitmapFont.KerningPairs = new Dictionary<KerningPair, int>(count);

                        for (var i = 0; i < count; i++)
                        {
                            var kerningLineSegments = TextFormatUtility.GetSegments(textReader.ReadLine());
                            var kerningPair = KerningPair.ReadText(kerningLineSegments, out var amount);
                            if (bitmapFont.KerningPairs.ContainsKey(kerningPair)) continue;
                            bitmapFont.KerningPairs[kerningPair] = amount;
                        }

                        break;
                    }
            }
        }

        return bitmapFont;
    }

    public static BitmapFont FromStream(Stream stream, FormatHint formatHint, bool leaveOpen)
    {
        switch (formatHint)
        {
            case FormatHint.Binary:
                using (var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen))
                    return ReadBinary(binaryReader);
            case FormatHint.XML:
                using (var streamReader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen))
                    return ReadXML(streamReader);
            case FormatHint.Text:
                using (var streamReader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen))
                    return ReadText(streamReader);
            default:
                throw new ArgumentOutOfRangeException(nameof(formatHint), formatHint, null);
        }
    }
    public static BitmapFont FromStream(Stream stream, bool leaveOpen)
    {
        using (var binaryReader = new BinaryReader(stream, Encoding.UTF8, true))
        {
            if (binaryReader.PeekChar() == MagicOne)
            {
                var bitmapFont = ReadBinary(binaryReader);
                if (!leaveOpen)
                {
                    stream.Dispose();
                }
                return bitmapFont;
            }
        }

        using (var streamReader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen))
        {
            return streamReader.Peek() == '<' ? ReadXML(streamReader) : ReadText(streamReader);
        }
    }

    public static BitmapFont FromFile(string path, FormatHint formatHint)
    {
        using (var fileStream = File.OpenRead(path))
        {
            return FromStream(fileStream, formatHint, true);
        }
    }
    public static BitmapFont FromFile(string path)
    {
        using (var fileStream = File.OpenRead(path))
        {
            return FromStream(fileStream, true);
        }
    }
}


// BitmapFontCommon.cs

public sealed class BitmapFontCommon
{
    public const int SizeInBytes = 15;

    public int LineHeight { get; set; }

    public int Base { get; set; }

    public int ScaleWidth { get; set; }

    public int ScaleHeight { get; set; }

    public bool Packed { get; set; }

    public ChannelData AlphaChannel { get; set; }

    public ChannelData RedChannel { get; set; }

    public ChannelData GreenChannel { get; set; }

    public ChannelData BlueChannel { get; set; }

    public static BitmapFontCommon ReadBinary(BinaryReader binaryReader, out int pageCount)
    {
        if (binaryReader.ReadInt32() != SizeInBytes)
        {
            throw new InvalidDataException("Invalid common block size.");
        }

        var binary = new BitmapFontCommon();

        binary.LineHeight = binaryReader.ReadUInt16();
        binary.Base = binaryReader.ReadUInt16();
        binary.ScaleWidth = binaryReader.ReadUInt16();
        binary.ScaleHeight = binaryReader.ReadUInt16();

        pageCount = binaryReader.ReadUInt16();

        var flags = binaryReader.ReadByte();
        binary.Packed = UtilityExtensions.IsBitSet(flags, 0);
        binary.AlphaChannel = (ChannelData)binaryReader.ReadByte();
        binary.RedChannel = (ChannelData)binaryReader.ReadByte();
        binary.GreenChannel = (ChannelData)binaryReader.ReadByte();
        binary.BlueChannel = (ChannelData)binaryReader.ReadByte();

        return binary;
    }
    public static BitmapFontCommon ReadXML(XElement element, out int pages)
    {
        var bitmapFontCommon = new BitmapFontCommon();

        bitmapFontCommon.LineHeight = (int?)element.Attribute("lineHeight") ?? 0;
        bitmapFontCommon.Base = (int?)element.Attribute("base") ?? 0;
        bitmapFontCommon.ScaleWidth = (int?)element.Attribute("scaleW") ?? 0;
        bitmapFontCommon.ScaleHeight = (int?)element.Attribute("scaleH") ?? 0;

        pages = (int?)element.Attribute("pages") ?? 0;

        bitmapFontCommon.Packed = (bool?)element.Attribute("packed") ?? false;

        bitmapFontCommon.AlphaChannel = UtilityExtensions.GetEnumValue<ChannelData>(element.Attribute("alphaChnl"));
        if (bitmapFontCommon.AlphaChannel == null) bitmapFontCommon.AlphaChannel = ChannelData.Glyph;
        bitmapFontCommon.RedChannel = UtilityExtensions.GetEnumValue<ChannelData>(element.Attribute("redChnl"));
        if (bitmapFontCommon.RedChannel == null) bitmapFontCommon.RedChannel = ChannelData.Glyph;
        bitmapFontCommon.GreenChannel = UtilityExtensions.GetEnumValue<ChannelData>(element.Attribute("greenChnl"));
        if (bitmapFontCommon.GreenChannel == null) bitmapFontCommon.GreenChannel = ChannelData.Glyph;
        bitmapFontCommon.BlueChannel = UtilityExtensions.GetEnumValue<ChannelData>(element.Attribute("blueChnl"));
        if (bitmapFontCommon.BlueChannel == null) bitmapFontCommon.BlueChannel = ChannelData.Glyph;

        return bitmapFontCommon;
    }
    public static BitmapFontCommon ReadText(IReadOnlyList<string> lineSegments, out int pages)
    {
        var bitmapFontCommon = new BitmapFontCommon();

        bitmapFontCommon.LineHeight = TextFormatUtility.ReadInt("lineHeight", lineSegments);
        bitmapFontCommon.Base = TextFormatUtility.ReadInt("base", lineSegments);
        bitmapFontCommon.ScaleWidth = TextFormatUtility.ReadInt("scaleW", lineSegments);
        bitmapFontCommon.ScaleHeight = TextFormatUtility.ReadInt("scaleH", lineSegments);

        pages = TextFormatUtility.ReadInt("pages", lineSegments);

        bitmapFontCommon.Packed = TextFormatUtility.ReadBool("packed", lineSegments);

        bitmapFontCommon.AlphaChannel = TextFormatUtility.ReadEnum<ChannelData>("alphaChnl", lineSegments);
        bitmapFontCommon.RedChannel = TextFormatUtility.ReadEnum<ChannelData>("redChnl", lineSegments);
        bitmapFontCommon.GreenChannel = TextFormatUtility.ReadEnum<ChannelData>("greenChnl", lineSegments);
        bitmapFontCommon.BlueChannel = TextFormatUtility.ReadEnum<ChannelData>("blueChnl", lineSegments);

        return bitmapFontCommon;
    }
}


// BitmapFontInfo.cs
public sealed class BitmapFontInfo
{
    public const int MinSizeInBytes = 15;

    public int Size { get; set; }

    public bool Smooth { get; set; }
    public bool Unicode { get; set; }
    public bool Italic { get; set; }
    public bool Bold { get; set; }

    public string Charset { get; set; }
    public int StretchHeight { get; set; }
    public int SuperSamplingLevel { get; set; }

    public int PaddingUp { get; set; }
    public int PaddingRight { get; set; }
    public int PaddingDown { get; set; }
    public int PaddingLeft { get; set; }

    public int SpacingHorizontal { get; set; }
    public int SpacingVertical { get; set; }

    public int Outline { get; set; }
    public string Face { get; set; }

    public static BitmapFontInfo ReadBinary(BinaryReader binaryReader)
    {
        if (binaryReader.ReadInt32() < MinSizeInBytes)
        {
            throw new InvalidDataException("Invalid info block size.");
        }

        var bitmapFontInfo = new BitmapFontInfo();

        bitmapFontInfo.Size = binaryReader.ReadInt16();

        var bitField = binaryReader.ReadByte();

        bitmapFontInfo.Smooth = UtilityExtensions.IsBitSet(bitField, 7);
        bitmapFontInfo.Unicode = UtilityExtensions.IsBitSet(bitField, 6);
        bitmapFontInfo.Italic = UtilityExtensions.IsBitSet(bitField, 5);
        bitmapFontInfo.Bold = UtilityExtensions.IsBitSet(bitField, 4);

        var characterSet = (CharacterSet)binaryReader.ReadByte();
        bitmapFontInfo.Charset = characterSet.ToString().ToUpper();

        bitmapFontInfo.StretchHeight = binaryReader.ReadUInt16();
        bitmapFontInfo.SuperSamplingLevel = binaryReader.ReadByte();

        bitmapFontInfo.PaddingUp = binaryReader.ReadByte();
        bitmapFontInfo.PaddingRight = binaryReader.ReadByte();
        bitmapFontInfo.PaddingDown = binaryReader.ReadByte();
        bitmapFontInfo.PaddingLeft = binaryReader.ReadByte();

        bitmapFontInfo.SpacingHorizontal = binaryReader.ReadByte();
        bitmapFontInfo.SpacingVertical = binaryReader.ReadByte();

        bitmapFontInfo.Outline = binaryReader.ReadByte();
        bitmapFontInfo.Face = UtilityExtensions.ReadNullTerminatedString(binaryReader);

        return bitmapFontInfo;
    }
    public static BitmapFontInfo ReadXML(XElement element)
    {
        var bitmapFontInfo = new BitmapFontInfo();

        bitmapFontInfo.Face = (string)element.Attribute("face") ?? string.Empty;
        bitmapFontInfo.Size = (int?)element.Attribute("size") ?? 0;
        bitmapFontInfo.Bold = (bool?)element.Attribute("bold") ?? false;
        bitmapFontInfo.Italic = (bool?)element.Attribute("italic") ?? false;

        bitmapFontInfo.Charset = (string)element.Attribute("charset") ?? string.Empty;

        bitmapFontInfo.Unicode = (bool?)element.Attribute("unicode") ?? false;
        bitmapFontInfo.StretchHeight = (int?)element.Attribute("stretchH") ?? 0;
        bitmapFontInfo.Smooth = (bool?)element.Attribute("smooth") ?? false;
        bitmapFontInfo.SuperSamplingLevel = (int?)element.Attribute("aa") ?? 0;

        var padding = ((string)element.Attribute("padding"))?.Split(',');
        if (padding != null)
        {
            bitmapFontInfo.PaddingLeft = padding.Length > 3 ? int.Parse(padding[3]) : 0;
            bitmapFontInfo.PaddingDown = padding.Length > 2 ? int.Parse(padding[2]) : 0;
            bitmapFontInfo.PaddingRight = padding.Length > 1 ? int.Parse(padding[1]) : 0;
            bitmapFontInfo.PaddingUp = padding.Length > 0 ? int.Parse(padding[0]) : 0;
        }

        var spacing = ((string)element.Attribute("spacing"))?.Split(',');
        if (spacing != null)
        {
            bitmapFontInfo.SpacingVertical = spacing.Length > 1 ? int.Parse(spacing[1]) : 0;
            bitmapFontInfo.SpacingHorizontal = spacing.Length > 0 ? int.Parse(spacing[0]) : 0;
        }

        bitmapFontInfo.Outline = (int?)element.Attribute("outline") ?? 0;

        return bitmapFontInfo;
    }
    public static BitmapFontInfo ReadText(IReadOnlyList<string> lineSegments)
    {
        var bitmapFontInfo = new BitmapFontInfo();

        bitmapFontInfo.Face = TextFormatUtility.ReadString("face", lineSegments, string.Empty);
        bitmapFontInfo.Size = TextFormatUtility.ReadInt("size", lineSegments);
        bitmapFontInfo.Bold = TextFormatUtility.ReadBool("bold", lineSegments);
        bitmapFontInfo.Italic = TextFormatUtility.ReadBool("italic", lineSegments);

        bitmapFontInfo.Charset = TextFormatUtility.ReadString("charset", lineSegments, string.Empty);

        bitmapFontInfo.Unicode = TextFormatUtility.ReadBool("unicode", lineSegments);
        bitmapFontInfo.StretchHeight = TextFormatUtility.ReadInt("stretchH", lineSegments);
        bitmapFontInfo.Smooth = TextFormatUtility.ReadBool("smooth", lineSegments);
        bitmapFontInfo.SuperSamplingLevel = TextFormatUtility.ReadInt("aa", lineSegments);

        var padding = TextFormatUtility.ReadValue("padding", lineSegments)?.Split(',');
        if (padding != null)
        {
            bitmapFontInfo.PaddingLeft = padding.Length > 3 ? int.Parse(padding[3]) : 0;
            bitmapFontInfo.PaddingDown = padding.Length > 2 ? int.Parse(padding[2]) : 0;
            bitmapFontInfo.PaddingRight = padding.Length > 1 ? int.Parse(padding[1]) : 0;
            bitmapFontInfo.PaddingUp = padding.Length > 0 ? int.Parse(padding[0]) : 0;
        }

        var spacing = TextFormatUtility.ReadValue("spacing", lineSegments)?.Split(',');
        if (spacing != null)
        {
            bitmapFontInfo.SpacingVertical = spacing.Length > 1 ? int.Parse(spacing[1]) : 0;
            bitmapFontInfo.SpacingHorizontal = spacing.Length > 0 ? int.Parse(spacing[0]) : 0;
        }

        bitmapFontInfo.Outline = TextFormatUtility.ReadInt("outline", lineSegments);

        return bitmapFontInfo;
    }
}



// BlockID.cs//**************************************************************************************************
// BlockID.cs                                                                                      *
// Copyright (c) 2018-2020 Aurora Berta-Oldham                                                     *
// This code is made available under the MIT License.                                              *
//**************************************************************************************************

public enum BlockID
{
    Info = 1, Common = 2, Pages = 3, Characters = 4, KerningPairs = 5
}


// Channel.cs//**************************************************************************************************
// Channel.cs                                                                                      *
// Copyright (c) 2018-2020 Aurora Berta-Oldham                                                     *
// This code is made available under the MIT License.                                              *
//**************************************************************************************************

public enum Channel
{
    None = 0, Blue = 1, Green = 2, Red = 4, Alpha = 8, All = 15
}


// ChannelData.cs//**************************************************************************************************
// ChannelData.cs                                                                                  *
// Copyright (c) 2018-2020 Aurora Berta-Oldham                                                     *
// This code is made available under the MIT License.                                              *
//**************************************************************************************************

public enum ChannelData
{
    Glyph = 0, Outline = 1, GlyphAndOutline = 2, Zero = 3, One = 4
}


// Character.cs

public sealed class Character
{
    public const int SizeInBytes = 20;

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int XOffset { get; set; }

    public int YOffset { get; set; }

    public int XAdvance { get; set; }

    public int Page { get; set; }

    public Channel Channel { get; set; }

    public static Character ReadBinary(BinaryReader binaryReader, out int id)
    {
        id = (int)binaryReader.ReadUInt32();

        return new Character
        {
            X = binaryReader.ReadUInt16(),
            Y = binaryReader.ReadUInt16(),
            Width = binaryReader.ReadUInt16(),
            Height = binaryReader.ReadUInt16(),
            XOffset = binaryReader.ReadInt16(),
            YOffset = binaryReader.ReadInt16(),
            XAdvance = binaryReader.ReadInt16(),
            Page = binaryReader.ReadByte(),
            Channel = (Channel)binaryReader.ReadByte()
        };
    }
    public static Character ReadXML(XElement element, out int id)
    {
        id = (int?)element.Attribute("id") ?? 0;

        var channel = UtilityExtensions.GetEnumValue<Channel>(element.Attribute("chnl"));
        if (channel == null) channel = Channel.None;
        return new Character
        {
            X = (int?)element.Attribute("x") ?? 0,
            Y = (int?)element.Attribute("y") ?? 0,
            Width = (int?)element.Attribute("width") ?? 0,
            Height = (int?)element.Attribute("height") ?? 0,
            XOffset = (int?)element.Attribute("xoffset") ?? 0,
            YOffset = (int?)element.Attribute("yoffset") ?? 0,
            XAdvance = (int?)element.Attribute("xadvance") ?? 0,
            Page = (int?)element.Attribute("page") ?? 0,
            Channel = channel
        };
    }
    public static Character ReadText(IReadOnlyList<string> lineSegments, out int id)
    {
        id = TextFormatUtility.ReadInt("id", lineSegments);

        return new Character
        {
            X = TextFormatUtility.ReadInt("x", lineSegments),
            Y = TextFormatUtility.ReadInt("y", lineSegments),
            Width = TextFormatUtility.ReadInt("width", lineSegments),
            Height = TextFormatUtility.ReadInt("height", lineSegments),
            XOffset = TextFormatUtility.ReadInt("xoffset", lineSegments),
            YOffset = TextFormatUtility.ReadInt("yoffset", lineSegments),
            XAdvance = TextFormatUtility.ReadInt("xadvance", lineSegments),
            Page = TextFormatUtility.ReadInt("page", lineSegments),
            Channel = TextFormatUtility.ReadEnum<Channel>("chnl", lineSegments)
        };
    }
}


// CharacterSet.cs//**************************************************************************************************
// CharacterSet.cs                                                                                 *
// Copyright (c) 2018-2020 Aurora Berta-Oldham                                                     *
// This code is made available under the MIT License.                                              *
//**************************************************************************************************

public enum CharacterSet
{
    ANSI = 0,
    Default = 1,
    Symbol = 2,
    Mac = 77,
    ShiftJIS = 128,
    Hangul = 129,
    Johab = 130,
    GB2312 = 134,
    ChineseBig5 = 136,
    Greek = 161,
    Turkish = 162,
    Vietnamese = 163,
    Hebrew = 177,
    Arabic = 178,
    Baltic = 186,
    Russian = 204,
    Thai = 222,
    EastEurope = 238,
    OEM = 255
}


// FormatHint.cs//**************************************************************************************************
// FormatHint.cs                                                                                   *
// Copyright (c) 2018-2020 Aurora Berta-Oldham                                                     *
// This code is made available under the MIT License.                                              *
//**************************************************************************************************

public enum FormatHint
{
    Binary, XML, Text
}


// KerningPair.cs
public struct KerningPair : IEquatable<KerningPair>
{
    public const int SizeInBytes = 10;

    public int First { get; }

    public int Second { get; }

    public KerningPair(int first, int second)
    {
        First = first;
        Second = second;
    }

    public bool Equals(KerningPair other)
    {
        return First == other.First && Second == other.Second;
    }
    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        return obj is KerningPair pair && Equals(pair);
    }
    public override int GetHashCode()
    {
        unchecked
        {
            return (First.GetHashCode() * 397) ^ Second.GetHashCode();
        }
    }
    public static bool operator ==(KerningPair left, KerningPair right)
    {
        return left.Equals(right);
    }
    public static bool operator !=(KerningPair left, KerningPair right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{nameof(First)}: {First}, {nameof(Second)}: {Second}";
    }

    public static KerningPair ReadBinary(BinaryReader binaryReader, out int amount)
    {
        var first = (int)binaryReader.ReadUInt32();
        var second = (int)binaryReader.ReadUInt32();
        amount = binaryReader.ReadInt16();

        return new KerningPair(first, second);
    }
    public static KerningPair ReadXML(XElement element, out int amount)
    {
        var first = (int?)element.Attribute("first") ?? 0;
        var second = (int?)element.Attribute("second") ?? 0;
        amount = (int?)element.Attribute("amount") ?? 0;

        return new KerningPair(first, second);
    }
    public static KerningPair ReadText(IReadOnlyList<string> lineSegments, out int amount)
    {
        var first = TextFormatUtility.ReadInt("first", lineSegments);
        var second = TextFormatUtility.ReadInt("second", lineSegments);
        amount = TextFormatUtility.ReadInt("amount", lineSegments);

        return new KerningPair(first, second);
    }
}


// TextFormatUtility.cs

internal static class TextFormatUtility
{
    public static IReadOnlyList<string> GetSegments(string line)
    {
        var ignoreWhiteSpace = false;
        var segments = new List<string>(16);
        var stringBuilder = new StringBuilder(16);

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            var endSegment = character == ' ' && !ignoreWhiteSpace;

            if (!endSegment)
            {
                if (character == '\"')
                {
                    ignoreWhiteSpace = !ignoreWhiteSpace;
                }
                else
                {
                    stringBuilder.Append(character);
                }
            }

            if ((endSegment || i == line.Length - 1) && stringBuilder.Length > 0)
            {
                segments.Add(stringBuilder.ToString());
                stringBuilder.Clear();
            }
        }

        return segments;
    }

    public static string ReadValue(string propertyName, IReadOnlyList<string> segments)
    {
        foreach (var segment in segments)
        {
            var equalsSign = segment.IndexOf('=');

            if (equalsSign != propertyName.Length) continue;

            if (string.Compare(segment, 0, propertyName, 0, equalsSign, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return segment.Remove(0, equalsSign + 1);
            }
        }

        return null;
    }
    public static bool ReadBool(string propertyName, IReadOnlyList<string> segments, bool missingValue = false)
    {
        var value = ReadValue(propertyName, segments);

        switch (value)
        {
            case null:
                return missingValue;
            case "1":
                return true;
            case "0":
                return false;
            default:
                // True and false aren't valid but might as well try to use them anyway.
                return Convert.ToBoolean(value);
        }
    }
    public static int ReadInt(string propertyName, IReadOnlyList<string> segments, int missingValue = 0)
    {
        var value = ReadValue(propertyName, segments);
        return value != null ? int.Parse(value) : missingValue;
    }
    public static string ReadString(string propertyName, IReadOnlyList<string> segments, string missingValue = null)
    {
        return ReadValue(propertyName, segments) ?? missingValue;
    }
    public static T ReadEnum<T>(string propertyName, IReadOnlyList<string> segments, T missingValue = default) where T : Enum
    {
        var value = ReadValue(propertyName, segments);
        return value != null ? (T)Enum.ToObject(typeof(T), int.Parse(value)) : missingValue;
    }
}


// UtilityExtensions.cs

public static class UtilityExtensions
{
    public static bool IsBitSet(byte @byte, int index)
    {
        if (index < 0 || index > 7) throw new ArgumentOutOfRangeException(nameof(index));
        return (@byte & (1 << index)) != 0;
    }

    public static byte SetBit(byte @byte, int index, bool set)
    {
        if (index < 0 || index > 7) throw new ArgumentOutOfRangeException(nameof(index));

        if (set)
        {
            return (byte)(@byte | (1 << index));
        }

        return (byte)(@byte & ~(1 << index));
    }

    public static string ReadNullTerminatedString(BinaryReader binaryReader)
    {
        var stringBuilder = new StringBuilder();

        while (true)
        {
            var character = binaryReader.ReadByte();

            if (character == 0)
            {
                break;
            }

            stringBuilder.Append((char)character);
        }

        return stringBuilder.ToString();
    }

    public static void WriteNullTerminatedString(BinaryWriter binaryWriter, string value)
    {
        if (value != null)
        {
            foreach (var character in value)
            {
                binaryWriter.Write((byte)character);
            }
        }

        binaryWriter.Write((byte)0);
    }

    public static T GetEnumValue<T>(XAttribute xAttribute) where T : Enum
    {
        if (xAttribute == null) return default(T);
        return (T)Enum.Parse(typeof(T), xAttribute.Value);
    }
}


#endregion