using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace BatteryReportApp
{
    internal sealed class DeviceRow
    {
        public string DeviceId, Serial, User, RecentUser, Model, Ou, Health, BatteryHealth, Cycles, ReportTime, BatteryState;
        public int Rank;
    }

    internal sealed class ReportForm : Form
    {
        private readonly string workbookPath;
        private readonly Color navy = Color.FromArgb(24, 42, 66);
        private readonly Color ink = Color.FromArgb(33, 48, 65);
        private readonly Color muted = Color.FromArgb(103, 119, 137);
        private readonly Color canvas = Color.FromArgb(244, 247, 251);
        private readonly List<DeviceRow> devices = new List<DeviceRow>();
        private readonly TextBox search = new TextBox();
        private readonly ComboBox healthFilter = new ComboBox();
        private readonly ComboBox ouFilter = new ComboBox();
        private readonly ComboBox sortFilter = new ComboBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly ToolStripStatusLabel status = new ToolStripStatusLabel();
        private readonly Label totalLabel = new Label();
        private readonly Dictionary<string, Label> metricValues = new Dictionary<string, Label>();
        private string telemetryCsvPath, inventoryCsvPath;
        private bool autoDetectCsvSources;
        private readonly string sourcePathsFile;

        public ReportForm(string path)
        {
            workbookPath = path;
            sourcePathsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BatteryReport.sources");
            LoadSourcePaths();
            Text = "GooseTech Chrome OS Battery Report";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1120, 700);
            Size = new Size(1440, 900);
            BackColor = canvas;
            Font = new Font("Segoe UI", 9F);
            BuildLayout();
            Shown += delegate { if (File.Exists(workbookPath)) RefreshReport(); else ImportExports(); };
        }

        private void LoadSourcePaths()
        {
            if (File.Exists(sourcePathsFile))
            {
                string[] paths = File.ReadAllLines(sourcePathsFile);
                if (paths.Length >= 2 && File.Exists(paths[0]) && File.Exists(paths[1]))
                {
                    telemetryCsvPath = paths[0]; inventoryCsvPath = paths[1];
                    return;
                }
            }

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parentDirectory = Directory.GetParent(appDirectory) == null ? appDirectory : Directory.GetParent(appDirectory).FullName;
            string workDirectory = Path.Combine(parentDirectory, "work");
            string inventory = Path.Combine(workDirectory, "fleet_inventory.csv");
            string telemetry = FindLatestTelemetryCsv(workDirectory);
            if (File.Exists(inventory) && File.Exists(telemetry))
            {
                inventoryCsvPath = inventory; telemetryCsvPath = telemetry; autoDetectCsvSources = true;
            }
        }

        private static string FindLatestTelemetryCsv(string directory)
        {
            if (!Directory.Exists(directory)) return null;
            return Directory.GetFiles(directory, "fleet_crosstelemetry*.csv")
                .Where(file => new FileInfo(file).Length > 0)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private void SaveSourcePaths()
        {
            File.WriteAllLines(sourcePathsFile, new[] { telemetryCsvPath ?? "", inventoryCsvPath ?? "" });
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = canvas, Margin = new Padding(0), Padding = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            Controls.Add(layout);

            var header = new Panel { Dock = DockStyle.Fill, BackColor = navy, Padding = new Padding(28, 7, 24, 5), Margin = new Padding(0) };
            var headerText = new Panel { Dock = DockStyle.Left, Width = 700, BackColor = navy };
            var title = new Label { Text = "GooseTech Chrome OS Battery Report", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 19F), Location = new Point(0, 1) };
            var subtitle = new Label { Text = "Fleet health  •  showing each device’s most recent user", AutoSize = true, ForeColor = Color.FromArgb(205, 220, 237), Location = new Point(3, 41) };
            headerText.Controls.Add(title); headerText.Controls.Add(subtitle); header.Controls.Add(headerText);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 252, Height = 42, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = navy, Padding = new Padding(0, 5, 0, 0) };
            var import = new Button { Text = "Import CSVs", Size = new Size(112, 32), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = navy, Margin = new Padding(0, 0, 8, 0) };
            import.FlatAppearance.BorderColor = Color.FromArgb(150,176,204); import.Click += delegate { ImportExports(); };
            var refresh = new Button { Text = "Refresh", Size = new Size(92, 32), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = navy, Margin = new Padding(0) };
            refresh.FlatAppearance.BorderColor = Color.FromArgb(150,176,204); refresh.Click += delegate { RefreshReport(); };
            actions.Controls.Add(import); actions.Controls.Add(refresh); header.Controls.Add(actions);
            layout.Controls.Add(header, 0, 0);

            var metrics = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 12, 7), BackColor = canvas, WrapContents = false, Margin = new Padding(0) };
            AddMetric(metrics, "Bad", Color.FromArgb(252, 228, 228), Color.FromArgb(165, 40, 40));
            AddMetric(metrics, "Medium", Color.FromArgb(255, 242, 213), Color.FromArgb(147, 98, 0));
            AddMetric(metrics, "Good", Color.FromArgb(225, 242, 228), Color.FromArgb(40, 107, 56));
            AddMetric(metrics, "No telemetry", Color.FromArgb(231, 235, 240), Color.FromArgb(85, 98, 115));
            totalLabel.AutoSize = true; totalLabel.ForeColor = muted; totalLabel.Margin = new Padding(8, 18, 0, 0); metrics.Controls.Add(totalLabel);
            layout.Controls.Add(metrics, 0, 1);

            var filters = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 5, 24, 5), BackColor = Color.White, ColumnCount = 4, RowCount = 2, Margin = new Padding(0) };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 18)); filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddFilter(filters, "Search", search, 0);
            healthFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            healthFilter.Items.AddRange(new object[] { "All health statuses", "Bad", "Medium", "Unknown", "Good", "No telemetry" }); healthFilter.SelectedIndex = 0;
            AddFilter(filters, "Health", healthFilter, 1);
            ouFilter.DropDownStyle = ComboBoxStyle.DropDownList; ouFilter.Items.Add("All organizational units"); ouFilter.SelectedIndex = 0;
            AddFilter(filters, "Organizational unit", ouFilter, 2);
            sortFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            sortFilter.Items.AddRange(new object[] { "Health: worst to best", "Health: best to worst", "Serial number", "User", "Organizational unit", "Make / model" }); sortFilter.SelectedIndex = 0;
            AddFilter(filters, "Sort", sortFilter, 3);
            search.TextChanged += delegate { ApplyFilters(); }; healthFilter.SelectedIndexChanged += delegate { ApplyFilters(); };
            ouFilter.SelectedIndexChanged += delegate { ApplyFilters(); }; sortFilter.SelectedIndexChanged += delegate { ApplyFilters(); };
            layout.Controls.Add(filters, 0, 2);

            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false; grid.AutoGenerateColumns = true; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(232, 237, 243); grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
            grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 241, 248);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ink; grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            grid.ColumnHeadersHeight = 38; grid.RowTemplate.Height = 31; grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(215, 231, 249);
            grid.DefaultCellStyle.SelectionForeColor = ink; grid.CellFormatting += FormatHealthCell;
            var tableHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 8), BackColor = canvas, Margin = new Padding(0) };
            tableHost.Controls.Add(grid); layout.Controls.Add(tableHost, 0, 3);

            var footer = new StatusStrip { BackColor = Color.White };
            status.Text = "Health categories are based on Google battery telemetry."; footer.Items.Add(status); footer.Dock = DockStyle.Fill;
            layout.Controls.Add(footer, 0, 4);
        }

        private void AddMetric(FlowLayoutPanel host, string label, Color fill, Color labelColor)
        {
            var card = new Panel { Size = new Size(164, 58), BackColor = fill, Margin = new Padding(0, 0, 10, 0) };
            var name = new Label { Text = label.ToUpperInvariant(), Location = new Point(12, 6), AutoSize = true, ForeColor = labelColor, Font = new Font("Segoe UI Semibold", 8F) };
            var value = new Label { Text = "—", Location = new Point(12, 22), AutoSize = true, ForeColor = ink, Font = new Font("Segoe UI Semibold", 16F) };
            card.Controls.Add(name); card.Controls.Add(value); host.Controls.Add(card); metricValues[label] = value;
        }

        private void AddFilter(TableLayoutPanel host, string label, Control control, int column)
        {
            var caption = new Label { Text = label, ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 1, 16, 0);
            host.Controls.Add(caption, column, 0); host.Controls.Add(control, column, 1);
        }

        private static int ColumnLetters(string cellReference)
        {
            int result = 0;
            foreach (char c in cellReference)
            {
                if (!Char.IsLetter(c)) break;
                result = result * 26 + (Char.ToUpperInvariant(c) - 'A' + 1);
            }
            return result;
        }

        private static XmlDocument ReadXml(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName);
            if (entry == null) return null;
            var document = new XmlDocument();
            using (var stream = entry.Open()) document.Load(stream);
            return document;
        }

        private static string[] ReadSharedStrings(XmlDocument document, XmlNamespaceManager ns)
        {
            if (document == null) return new string[0];
            return document.SelectNodes("//x:si", ns).Cast<XmlNode>()
                .Select(si => String.Concat(si.SelectNodes(".//x:t", ns).Cast<XmlNode>().Select(t => t.InnerText))).ToArray();
        }

        private static string ReadCell(XmlNode cell, XmlNamespaceManager ns, string[] shared)
        {
            if (cell == null) return "";
            string type = cell.Attributes["t"] == null ? "" : cell.Attributes["t"].Value;
            if (type == "inlineStr") return String.Concat(cell.SelectNodes(".//x:is/x:t", ns).Cast<XmlNode>().Select(t => t.InnerText));
            var value = cell.SelectSingleNode("x:v", ns);
            if (value == null) return "";
            if (type == "s")
            {
                int index;
                return Int32.TryParse(value.InnerText, out index) && index >= 0 && index < shared.Length ? shared[index] : "";
            }
            return value.InnerText;
        }

        private List<DeviceRow> ReadWorkbook()
        {
            var result = new List<DeviceRow>();
            using (var archive = ZipFile.OpenRead(workbookPath))
            {
                var ns = new XmlNamespaceManager(new NameTable()); ns.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                string[] shared = ReadSharedStrings(ReadXml(archive, "xl/sharedStrings.xml"), ns);
                var sheet = ReadXml(archive, "xl/worksheets/sheet1.xml");
                if (sheet == null) throw new InvalidDataException("The workbook does not contain its expected report sheet.");
                var rows = sheet.SelectNodes("//x:sheetData/x:row", ns).Cast<XmlNode>();
                foreach (var row in rows)
                {
                    int rowNumber;
                    if (!Int32.TryParse(row.Attributes["r"].Value, out rowNumber) || rowNumber < 11) continue;
                    var cells = new Dictionary<int, string>();
                    foreach (XmlNode cell in row.SelectNodes("x:c", ns))
                    {
                        string reference = cell.Attributes["r"].Value;
                        cells[ColumnLetters(reference)] = ReadCell(cell, ns, shared);
                    }
                    Func<int, string> value = index => cells.ContainsKey(index) ? cells[index] : "";
                    if (String.IsNullOrWhiteSpace(value(1))) continue;
                    string health = value(8);
                    double ratio;
                    string ratioText = Double.TryParse(value(9), NumberStyles.Any, CultureInfo.InvariantCulture, out ratio) ? ratio.ToString("P1", CultureInfo.CurrentCulture) : "";
                    string reportTime = value(13); double serialDate;
                    if (Double.TryParse(reportTime, NumberStyles.Any, CultureInfo.InvariantCulture, out serialDate))
                    {
                        try { reportTime = DateTime.FromOADate(serialDate).ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture); }
                        catch { reportTime = ""; }
                    }
                    int rank = health == "Bad" ? 0 : health == "Medium" ? 1 : health == "Unknown" ? 2 : health == "Good" ? 3 : 4;
                    result.Add(new DeviceRow { DeviceId=value(1), Serial=value(2), User=value(3), RecentUser=value(4), Model=value(5), Ou=value(6), Health=health,
                        BatteryHealth=ratioText, Cycles=value(12), ReportTime=reportTime, BatteryState=value(14), Rank=rank });
                }
            }
            return result;
        }

        private static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>(); var row = new List<string>(); var field = new System.Text.StringBuilder(); bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else field.Append(c);
                }
                else if (c == '"' && field.Length == 0) quoted = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r' || c == '\n')
                {
                    row.Add(field.ToString()); field.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                    row.Clear(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
            return rows;
        }

        private static Dictionary<string, int> HeaderIndex(string[] header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++) map[header[i].Trim().TrimStart('\uFEFF')] = i;
            return map;
        }

        private static string CsvValue(string[] row, Dictionary<string, int> map, string header)
        {
            int index;
            return map.TryGetValue(header, out index) && index < row.Length ? row[index].Trim() : "";
        }

        private List<DeviceRow> ReadCsvExports()
        {
            var telemetry = ParseCsv(File.ReadAllText(telemetryCsvPath));
            var inventory = ParseCsv(File.ReadAllText(inventoryCsvPath));
            if (telemetry.Count < 2 || inventory.Count < 2) throw new InvalidDataException("The selected CSV export is empty or has no data rows.");
            var ti = HeaderIndex(telemetry[0]); var di = HeaderIndex(inventory[0]);
            foreach (string header in new[] { "deviceId", "batteryInfo.0.designCapacity", "batteryStatusReport.0.fullChargeCapacity", "batteryStatusReport.0.batteryHealth" })
                if (!ti.ContainsKey(header)) throw new InvalidDataException("The telemetry CSV is missing the expected column: " + header);
            foreach (string header in new[] { "deviceId", "serialNumber", "model", "orgUnitPath", "mostRecentUser" })
                if (!di.ContainsKey(header)) throw new InvalidDataException("The device inventory CSV is missing the expected column: " + header);

            var telemetryById = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string[] row in telemetry.Skip(1))
            {
                string id = CsvValue(row, ti, "deviceId"); if (id.Length > 0) telemetryById[id] = row;
            }
            var result = new List<DeviceRow>();
            foreach (string[] device in inventory.Skip(1))
            {
                string id = CsvValue(device, di, "deviceId"); if (id.Length == 0) continue;
                string[] battery; telemetryById.TryGetValue(id, out battery); battery = battery ?? new string[0];
                string healthValue = CsvValue(battery, ti, "batteryStatusReport.0.batteryHealth");
                string health = healthValue == "BATTERY_REPLACE_NOW" ? "Bad" : healthValue == "BATTERY_REPLACE_SOON" ? "Medium" : healthValue == "BATTERY_HEALTH_NORMAL" ? "Good" :
                    String.IsNullOrWhiteSpace(healthValue) || healthValue == "BATTERY_HEALTH_UNSPECIFIED" ? "No telemetry" : "Unknown";
                double design, full;
                bool hasDesign = Double.TryParse(CsvValue(battery, ti, "batteryInfo.0.designCapacity"), NumberStyles.Any, CultureInfo.InvariantCulture, out design);
                bool hasFull = Double.TryParse(CsvValue(battery, ti, "batteryStatusReport.0.fullChargeCapacity"), NumberStyles.Any, CultureInfo.InvariantCulture, out full);
                string ratio = hasDesign && design != 0 && hasFull ? (full / design).ToString("P1", CultureInfo.CurrentCulture) : "";
                int rank = health == "Bad" ? 0 : health == "Medium" ? 1 : health == "Unknown" ? 2 : health == "Good" ? 3 : 4;
                result.Add(new DeviceRow {
                    DeviceId=id, Serial=CsvValue(device, di, "serialNumber"), User=CsvValue(device, di, "mostRecentUser"), RecentUser=CsvValue(device, di, "annotatedUser"),
                    Model=CsvValue(device, di, "model"), Ou=CsvValue(device, di, "orgUnitPath"), Health=health, BatteryHealth=ratio,
                    Cycles=CsvValue(battery, ti, "batteryStatusReport.0.cycleCount"), ReportTime=CsvValue(battery, ti, "batteryStatusReport.0.reportTime"),
                    BatteryState=CsvValue(battery, ti, "batteryStatusReport.0.sample.0.status"), Rank=rank
                });
            }
            return result;
        }

        private void ImportExports()
        {
            using (var dialog = new OpenFileDialog { Title = "Select the GAM battery telemetry CSV", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", CheckFileExists = true })
            {
                if (!String.IsNullOrWhiteSpace(telemetryCsvPath) && File.Exists(telemetryCsvPath)) dialog.FileName = telemetryCsvPath;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                telemetryCsvPath = dialog.FileName;
            }
            using (var dialog = new OpenFileDialog { Title = "Select the Google Admin device inventory CSV", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", CheckFileExists = true })
            {
                if (!String.IsNullOrWhiteSpace(inventoryCsvPath) && File.Exists(inventoryCsvPath)) dialog.FileName = inventoryCsvPath;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                inventoryCsvPath = dialog.FileName;
            }
            autoDetectCsvSources = false;
            SaveSourcePaths();
            RefreshReport();
        }

        private void RefreshReport()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                if (autoDetectCsvSources)
                {
                    string latestTelemetry = FindLatestTelemetryCsv(Path.GetDirectoryName(telemetryCsvPath));
                    if (!String.IsNullOrWhiteSpace(latestTelemetry)) telemetryCsvPath = latestTelemetry;
                }
                bool hasCsvSources = !String.IsNullOrWhiteSpace(telemetryCsvPath) && File.Exists(telemetryCsvPath) && !String.IsNullOrWhiteSpace(inventoryCsvPath) && File.Exists(inventoryCsvPath);
                status.Text = hasCsvSources ? "Reading the latest telemetry and inventory CSVs…" : "Reading the latest saved report…"; Refresh();
                devices.Clear(); devices.AddRange(hasCsvSources ? ReadCsvExports() : ReadWorkbook());
                foreach (var key in metricValues.Keys) metricValues[key].Text = devices.Count(d => d.Health == key).ToString(CultureInfo.CurrentCulture);
                totalLabel.Text = devices.Count.ToString(CultureInfo.CurrentCulture) + " devices in this snapshot";
                string previous = ouFilter.SelectedItem == null ? "All organizational units" : ouFilter.SelectedItem.ToString();
                ouFilter.Items.Clear(); ouFilter.Items.Add("All organizational units");
                foreach (string ou in devices.Select(d => d.Ou).Where(x => !String.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)) ouFilter.Items.Add(ou);
                ouFilter.SelectedItem = ouFilter.Items.Contains(previous) ? previous : "All organizational units";
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not read the battery report.\r\n\r\n" + ex.Message, "GooseTech Chrome OS Battery Report", MessageBoxButtons.OK, MessageBoxIcon.Error);
                status.Text = "Report could not be loaded.";
            }
            finally { Cursor = Cursors.Default; }
        }

        private void ApplyFilters()
        {
            if (devices.Count == 0 || healthFilter.SelectedItem == null || sortFilter.SelectedItem == null) return;
            string query = search.Text.Trim(); string health = healthFilter.SelectedItem.ToString(); string ou = ouFilter.SelectedItem == null ? "All organizational units" : ouFilter.SelectedItem.ToString();
            IEnumerable<DeviceRow> rows = devices.Where(d => (health == "All health statuses" || d.Health == health) && (ou == "All organizational units" || d.Ou == ou));
            if (query.Length > 0)
                rows = rows.Where(d => String.Join(" ", d.DeviceId,d.Serial,d.User,d.RecentUser,d.Model,d.Ou,d.Health,d.BatteryHealth,d.Cycles,d.ReportTime,d.BatteryState).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
            string sort = sortFilter.SelectedItem.ToString();
            if (sort == "Health: best to worst") rows = rows.OrderBy(d => d.Health == "Good" ? 0 : d.Health == "Medium" ? 1 : d.Health == "Bad" ? 2 : d.Health == "Unknown" ? 3 : 4).ThenBy(d => d.Serial);
            else if (sort == "Serial number") rows = rows.OrderBy(d => d.Serial, StringComparer.CurrentCultureIgnoreCase);
            else if (sort == "User") rows = rows.OrderBy(d => d.User, StringComparer.CurrentCultureIgnoreCase);
            else if (sort == "Organizational unit") rows = rows.OrderBy(d => d.Ou, StringComparer.CurrentCultureIgnoreCase);
            else if (sort == "Make / model") rows = rows.OrderBy(d => d.Model, StringComparer.CurrentCultureIgnoreCase);
            else rows = rows.OrderBy(d => d.Rank).ThenBy(d => d.BatteryHealth, StringComparer.CurrentCultureIgnoreCase).ThenBy(d => d.Serial);

            var table = new DataTable();
            foreach (string column in new[] { "Health", "Chromebook serial", "Most recent user", "Assigned user", "Make / model", "Organizational unit", "Battery health", "Cycle count", "Last report (UTC)", "Battery state", "Device ID" }) table.Columns.Add(column);
            foreach (DeviceRow d in rows)
                table.Rows.Add(d.Health,d.Serial,d.User,d.RecentUser,d.Model,d.Ou,d.BatteryHealth,d.Cycles,d.ReportTime,d.BatteryState,d.DeviceId);
            grid.DataSource = table;
            foreach (string name in new[] { "Device ID", "Assigned user", "Battery state" }) grid.Columns[name].Visible = false;
            grid.Columns["Health"].FillWeight = 65; grid.Columns["Chromebook serial"].FillWeight = 105; grid.Columns["Most recent user"].FillWeight = 155;
            grid.Columns["Make / model"].FillWeight = 145; grid.Columns["Organizational unit"].FillWeight = 165; grid.Columns["Battery health"].FillWeight = 85;
            grid.Columns["Cycle count"].FillWeight = 65; grid.Columns["Last report (UTC)"].FillWeight = 115;
            string refreshedAt = !String.IsNullOrWhiteSpace(telemetryCsvPath) && File.Exists(telemetryCsvPath)
                ? "  •  Telemetry CSV updated " + File.GetLastWriteTime(telemetryCsvPath).ToString("g", CultureInfo.CurrentCulture)
                : "";
            status.Text = "Showing " + table.Rows.Count.ToString(CultureInfo.CurrentCulture) + " of " + devices.Count.ToString(CultureInfo.CurrentCulture) + " devices  •  Click a column heading to sort" + refreshedAt;
        }

        private void FormatHealthCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Health") return;
            string health = Convert.ToString(e.Value, CultureInfo.CurrentCulture);
            var row = grid.Rows[e.RowIndex];
            if (health == "Bad") { row.DefaultCellStyle.BackColor = Color.FromArgb(252,228,228); e.CellStyle.ForeColor = Color.FromArgb(165,40,40); }
            else if (health == "Medium") { row.DefaultCellStyle.BackColor = Color.FromArgb(255,246,222); e.CellStyle.ForeColor = Color.FromArgb(147,98,0); }
            else if (health == "Good") { row.DefaultCellStyle.BackColor = Color.FromArgb(232,245,234); e.CellStyle.ForeColor = Color.FromArgb(40,107,56); }
            else { row.DefaultCellStyle.BackColor = Color.White; e.CellStyle.ForeColor = muted; }
            e.CellStyle.Font = new Font("Segoe UI Semibold", 9F);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChromeOS_All_Fleet_Battery_Status.xlsx");

            Application.Run(new ReportForm(path));
        }
    }
}
