using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Arvy;
using KamenReader;
using KamenReader.Excel;
using Ria;
using Tipe;
using Valerie;
using Varya;

namespace Kazuma;

public class ExcelReader {
    public void ReadAndExtractData(PipelineContext context) {
        var excelReader = new XmlConfigExcelReader("${var:BaseDir}\\Kazuma\\App_Data\\kazuma.config.xml".Resolve());
        IList<FileReaderResult> dataList = excelReader.Read("kazuma:reminder");
        IList<XmlWorksheetDefinition> wsheetConfigs =
            excelReader
                .SpreadsheetDefinitions
                .FirstOrDefault(ssheet => ssheet.Name == "kazuma:reminder")
                .Worksheets;

        // cannot use foreach on dataList beacuse of you may need different T to be used as resulting generic data
        Validated<TaskReminder> validated = ValidateThenExtractExcelData<TaskReminder>(wsheetConfigs, dataList[0]);

        var writer = new XmlSerializer(validated.ValidData.GetType());
        String xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kazuma-state.xml");
        using (FileStream file = File.Create(xmlPath))
            writer.Serialize(file, validated.ValidData);
    }

    Validated<T> ValidateThenExtractExcelData<T>(IList<XmlWorksheetDefinition> wsheetConfigs, FileReaderResult rawData) {
        // cleaning
        IList<GridData> cleansedData = rawData.CleanupRawData();

        // validation config
        XmlWorksheetDefinition wsheetConfig = wsheetConfigs.FirstOrDefault();
        IList<Func<String, GridData, Boolean, ActionResponseViewModel>> validators =
            wsheetConfig
                .Columns
                .Select(conf =>
                    CellValueValidator.Validators.ContainsKey(conf.Type) ?
                        CellValueValidator.Validators[conf.Type] :
                        CellValueValidator.Validators["No"])
                .ToList();

        // validate
        IList<String> names = wsheetConfig.Columns.Select(column => column.Name).ToList();
        IList<Boolean> allowEmpties = wsheetConfig.Columns.Select(column => column.AllowEmpty).ToList();
        IList<ActionResponseViewModel> validationMessages = cleansedData
            .Select(cleansed => validators[cleansed.Column -1]
                .Invoke(
                    names[cleansed.Column -1],
                    cleansed,
                    allowEmpties[cleansed.Column -1]))
            .ToList();

        var result = new Validated<T> {
            Messages = validationMessages,
            ContainsFail = validationMessages.Any(vmsg => vmsg.ResponseType == ActionResponseViewModel.Error),
            AllFails = validationMessages.All(vmsg => vmsg.ResponseType == ActionResponseViewModel.Error),
            ValidData = new List<T>()
        };

        // parser config
        IList<Func<String, GridData, DataType>> parsers =
            wsheetConfig
                .Columns
                .Select(conf =>
                    CellValueParser.Parsers.ContainsKey(conf.Type) ?
                        CellValueParser.Parsers[conf.Type] :
                        CellValueParser.Parsers["No"])
                .ToList();

        // parse
        IList<IGrouping<Int32, GridData>> groupList = cleansedData.GroupBy(prm => prm.Row).ToList();
        foreach (IGrouping<Int32, GridData> @group in groupList) {
            //var pResult = Activator.CreateInstance<T>();
            //var pResult = (T) Activator.CreateInstance(typeof(T), new Object[]{});
            var pResult = (T) FormatterServices.GetUninitializedObject(typeof(T));
            foreach (GridData raw in @group.ToList()) {
                if (!wsheetConfig.Columns[raw.Column -1].Process)
                    continue;

                DataType parsed = parsers[raw.Column - 1].Invoke(names[raw.Column - 1], raw);
                if (parsed == null)
                    continue;

                PopulateParsedData(ref pResult, parsed);
            }

            result.ValidData.Add(pResult);
        }

        return result;
    }

    void PopulateParsedData<T>(ref T pResult, DataType parsed) {
        Type type = pResult.GetType();
        PropertyInfo prop = type.GetProperty(parsed.Name);
        prop.SetValue(pResult, Convert.ChangeType(parsed.Value, parsed.Type), null);
    }
}
