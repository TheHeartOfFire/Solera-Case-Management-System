using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Utils;

public sealed record FormgenFieldInfo(
    int FieldNum,
    string VariableName,
    string Description,
    string Category,
    string ScreenId,
    string CobolPicture,
    string DefaultChar,
    int LeftSize,
    int RightSize,
    bool IsVehicleMerchandisingSafe,
    string? VehicleMerchandisingDescription,
    bool IsExplicitlyExcludedFromVm = false)
{
    public string Token => $"F{FieldNum}";

    public bool IsNumeric =>
        DefaultChar is "N" or "0" ||
        (!string.IsNullOrEmpty(CobolPicture) && CobolPicture.StartsWith('9'));

    public bool IsDateField =>
        VariableName.Contains("DATE", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
        (VehicleMerchandisingDescription?.Contains("Date", StringComparison.OrdinalIgnoreCase) ?? false);

    public string DataTypeSummary
    {
        get
        {
            if (IsNumeric)
            {
                return RightSize > 0
                    ? $"Decimal / Currency Number ({LeftSize} digits, {RightSize} decimals • COBOL {CobolPicture})"
                    : $"Whole Number ({LeftSize} digits • COBOL {CobolPicture})";
            }

            if (IsDateField)
            {
                return $"Date String ({LeftSize} chars • COBOL {CobolPicture})";
            }

            return $"Text / Alphanumeric (max {LeftSize} chars • COBOL {CobolPicture})";
        }
    }

    public string VmStatusBadge =>
        IsVehicleMerchandisingSafe
            ? "VM Safe ✓"
            : (IsExplicitlyExcludedFromVm ? "Not VM Safe ✕ (Vehicle Only = N)" : "Deal Only (Not in VM)");

    public string ShortSummary =>
        $"F{FieldNum}: {Description} ({VariableName}) [{VmStatusBadge}]";

    public string VerboseTooltip
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Field F{FieldNum} — {Description}");
            sb.AppendLine($"COBOL Variable: {VariableName}   |   Category: {Category} (Screen: {ScreenId})");
            sb.AppendLine($"Data Type: {DataTypeSummary}");
            if (IsVehicleMerchandisingSafe)
            {
                sb.Append($"Vehicle Merchandising (VM): ✓ SAFE — Available in Vehicle Merchandising ({VehicleMerchandisingDescription ?? Description}).");
            }
            else if (IsExplicitlyExcludedFromVm)
            {
                sb.Append("Vehicle Merchandising (VM): ✕ NOT SAFE — Explicitly marked 'Vehicle Only = N' in the Vehicle Merchandising field list.");
            }
            else
            {
                sb.Append("Vehicle Merchandising (VM): ⚠ Deal/FormGen Only — Not in the Vehicle Merchandising safe field subset.");
            }
            return sb.ToString();
        }
    }
}

public sealed record FormgenSmartFieldInfo(
    string Name,
    string Category,
    string Description,
    string? DefaultFormat = null)
{
    public string ShortSummary => $"{Name} ({Category}): {Description}";

    public string VerboseTooltip =>
        $"{Name}\nCategory: {Category}\nDescription: {Description}\n(Predefined FormGen Smart Field: reacts dynamically to deal data and prompts at print runtime)";
}

public sealed record FormgenFunctionDoc(
    string Prefix,
    string FullName,
    string Syntax,
    string HowItWorks,
    string Example,
    string? FormgenNotes = null)
{
    public string VerboseTooltip
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{FullName}");
            sb.AppendLine($"Syntax: {Syntax}");
            sb.AppendLine();
            sb.AppendLine($"How It Works: {HowItWorks}");
            sb.AppendLine($"Example: {Example}");
            if (!string.IsNullOrWhiteSpace(FormgenNotes))
            {
                sb.AppendLine();
                sb.Append($"FormGen Notes & Quirks: {FormgenNotes}");
            }
            return sb.ToString();
        }
    }
}

public sealed record FormgenOperatorDoc(
    string Symbol,
    string ShortName,
    string FullName,
    string Syntax,
    string HowItWorks,
    string Example,
    string? FormgenNotes = null)
{
    public string DropdownDisplay => $"{Symbol}  ({ShortName})";

    public string VerboseTooltip
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{FullName}");
            sb.AppendLine($"Syntax: {Syntax}");
            sb.AppendLine();
            sb.AppendLine($"How It Works: {HowItWorks}");
            sb.AppendLine($"Example: {Example}");
            if (!string.IsNullOrWhiteSpace(FormgenNotes))
            {
                sb.AppendLine();
                sb.Append($"FormGen Notes: {FormgenNotes}");
            }
            return sb.ToString();
        }
    }
}

public static class FormgenReferenceCatalog
{
    private sealed class RawJsonField
    {
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("screenId")] public string? ScreenId { get; set; }
        [JsonPropertyName("defaultChar")] public string? DefaultChar { get; set; }
        [JsonPropertyName("rightsize")] public int RightSize { get; set; }
        [JsonPropertyName("leftsize")] public int LeftSize { get; set; }
        [JsonPropertyName("cobolPicture")] public string? CobolPicture { get; set; }
        [JsonPropertyName("fieldDescription")] public string? FieldDescription { get; set; }
        [JsonPropertyName("fieldVariable")] public string? FieldVariable { get; set; }
        [JsonPropertyName("fieldNum")] public int FieldNum { get; set; }
    }

    // Complete Vehicle Merchandising (VM) field subset from Vehicle Merchandising Fields.xlsx
    private static readonly Dictionary<int, (string VmVar, string VmDesc, bool IsSafe)> VmFieldSubset = new()
    {
        [11] = ("DL-VEHCOST", "Vehicle Cost", true),
        [61] = ("VH-YEAR", "Sold Vehicle Year", true),
        [62] = ("VH-MAKE", "Sold Vehicle Make", true),
        [63] = ("VH-MODEL", "Sold Vehicle Model", true),
        [64] = ("VH-BODY", "Sold Vehicle Body", true),
        [65] = ("VH-COLOR", "Sold Vehicle Color", true),
        [66] = ("VH-NOCYLS", "Sold Vehicle Cylinders", true),
        [67] = ("VH-KEYNO-IGN", "Sold Vehicle Key No. Ignition", true),
        [68] = ("VH-KEYNO-TRK", "Sold Vehicle Key No. Trunk", true),
        [69] = ("VH-CUR-MILES", "Sold Vehicle Miles", true),
        [70] = ("DL-VEH-STOCKNO", "Sold Vehicle Stock Number", true),
        [71] = ("DL-VEH-VIN", "Sold Vehicle VIN Number", true),
        [72] = ("DL-VEH-NEW-USED", "New/Used/Demo/Whsl Code (N,U,D,W)", true),
        [91] = ("WS-BI-NAME", "Dealer Name", true),
        [92] = ("WS-BI-ADR1", "Dealer Street Address", true),
        [93] = ("WS-BI-CITY", "Dealer City", true),
        [94] = ("WS-BI-STATE", "Dealer State", true),
        [95] = ("WS-BI-ZIP", "Dealer Zip Code", true),
        [202] = ("DL-MSRP-AMT", "Total MSRP Amount (Marked Vehicle Only = N)", false),
        [292] = ("WS-BI-NAME2", "Dealer Name Line2", true),
        [293] = ("WS-BI-PHONE", "Dealer Phone Number", true),
        [373] = ("VH-WEIGHT", "Weight Of Sold Vehicle", true),
        [387] = ("WS-BI-DEALERNO", "Dealer Number", true),
        [388] = ("WS-BI-COUNTY", "Dealer County", true),
        [434] = ("VH-CAR-TRK", "Sold Vehicle Car/Truck Code", true),
        [435] = ("VH-TRANSMISSION", "Transmission (A=Auto, M=Manual)", true),
        [436] = ("VH-AIR-COND", "Air Conditioning (Y/N)", true),
        [437] = ("CONV-DATE", "Vehicle In Inventory (MM/DD/YY)", true),
        [438] = ("VH-MARKET-VALUE", "Vehicle Market Value", true),
        [446] = ("DL-ADV-AMT", "Advertising Amount", true),
        [477] = ("VH-PLATE-NO", "Sale Vehicle License Plate Number", true),
        [481] = ("CONV-DATE", "Sale Vehicle License Expire Date (MM/DD/YY)", true),
        [485] = ("CONV-DATE", "Wholesale Date Check written (MM/DD/YY)", true),
        [486] = ("CONV-DATE", "Wholesale Paid Date (MM/DD/YY)", true),
        [487] = ("CONV-DATE", "Wholesale Sale Date (MM/DD/YY)", true),
        [488] = ("VH-TITLE-RECVD", "Title Received (X=Received, P=Problem, A=Applied)", true),
        [489] = ("VH-PURCH-FROM-CO", "Vehicle Purchased From", true),
        [490] = ("VH-AUCT-SLD-THRU", "Auction Vehicle Sold Through", true),
        [491] = ("SALESMGR-LICNO(7)", "Name of person who purchased vehicle for dealership", true),
        [492] = ("DAYS-IN-STOCK", "Days in Inventory (Sale date minus date in inventory)", true),
        [510] = ("DL-HOLDBACK-AMT", "Holdback Amount", true),
        [762] = ("DL-VEH-VIN(12:6)", "Last 6 of the VIN # (Sold Veh)", true),
        [763] = ("DL-VEH-VIN(10:8)", "Last 8 of the VIN # (Sold Veh)", true),
        [775] = ("VH-ENGINE", "Vehicle Engine", true),
        [776] = ("VH-FUEL-TYPE", "Fuel Type", true),
        [777] = ("VH-TRIM", "Trim", true),
        [778] = ("VH-CERTIFIED-FLAG", "Certified Indicator", true),
        [779] = ("VH-KOV", "Kind Of Vehicle (KOV)", true),
        [780] = ("VH-VEH-SOURCE", "Vehicle Source Code", true),
        [781] = ("VH-INT-COLOR", "Interior Color", true),
        [782] = ("VH-COLOR-CD", "Color Code", true),
        [783] = ("VH-COMMERCIAL-FLAG", "Commercial Indicator", true),
        [784] = ("VH-FLEET-IND", "Fleet Indicator", true),
        [785] = ("VH-NO-OF-SPEEDS", "# Of Speeds", true),
        [786] = ("VH-NO-OF-DOORS", "# Of Doors", true),
        [787] = ("VH-NO-OF-PASS", "# Of Passengers", true),
        [788] = ("VH-KEYLESS-CD", "Keyless Code", true),
        [789] = ("VH-PREVTITLE-STATE", "Previous Title State", true),
        [790] = ("VH-PREVTITLE-NO", "Previous Title #", true),
        [791] = ("VH-SALESMAN", "Salesman Code - Vehicle Record", true),
        [792] = ("VH-WAR-DESC", "Warranty Description (from Vehicle Inventory File)", true),
        [793] = ("VH-WAREXPIR-DATE", "Warranty Expire Date (from vehicle inventory file)", true),
        [794] = ("VH-WAREXPIR-MILES", "Warranty Expire Miles (from vehicle inventory file)", true),
        [795] = ("VH-UDF1", "User Defined 1", true),
        [796] = ("VH-UDF2", "User Defined 2", true),
        [797] = ("VH-UDF3", "User Defined 3", true),
        [798] = ("VH-UDF4", "User Defined 4", true),
        [799] = ("VH-MODEL-NO", "Model #", true),
        [894] = ("DL-COST-BUMP-AMT", "Cost Bump Amount", true),
        [895] = ("DL-VEH-COMM-EXCL", "Vehicle Commission Exclusion", true),
        [937] = ("DL-TEMP-PERMIT", "Temporary Permit", true),
        [938] = ("DL-PREV-TITLE", "Previous Title", true),
        [939] = ("DL-PREV-TITLE-ST", "Previous Title State", true),
        [2224] = ("PR-VAR", "Stacks Dealer Name on Top when Dealer Name Line2 is Populated", true),
        [2225] = ("PR-VAR", "Stacks Dealer Name Line2 on bottom if it is populated", true),
        [2497] = ("WS-BI-DLR-WEBSITE", "Dealer Website", true),
        [2498] = ("WS-BI-DLR-EMAIL", "Dealer Email Address", true),
        [2499] = ("WS-BI-CITY, WS-BI-STATE WS-BI-ZIP", "Dealer City, State Zip (Combined)", true),
        [2500] = ("WS-BI-PHONE2", "Dealership Phone Line #2 (Fax)", true),
        [2857] = ("PR-VAR", "Prints wording for field 72 = New/Used/Demo/Whsl", true),
        [2887] = ("DL-SUBCO-9", "Subcompany number", true),
        [2888] = ("PREV-OWNER-STREET", "Prev owner street", true),
        [2889] = ("PREV-OWNER-CITY", "Prev owner city", true),
        [2890] = ("PREV-OWNER-STATE", "Prev owner state", true),
        [2891] = ("PREV-OWNER-ZIP", "Prev owner zip", true),
        [2913] = ("VH-UDF5", "user defined field #5", true),
        [2914] = ("VH-UDF6", "user defined field #6", true),
        [2915] = ("VH-UDF7", "user defined field #7", true),
        [2916] = ("VH-UDF8", "user defined field #8", true),
        [2917] = ("temp tag", "temp tag", true)
    };

    private static readonly Lazy<IReadOnlyDictionary<int, FormgenFieldInfo>> LazyFields = new(LoadMasterFields);

    public static IReadOnlyDictionary<int, FormgenFieldInfo> Fields => LazyFields.Value;

    // Complete Function Documentation from Formgen Formulas.xlsm (Base Functions)
    public static IReadOnlyDictionary<string, FormgenFunctionDoc> Functions { get; } =
        new Dictionary<string, FormgenFunctionDoc>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOT"] = new(
                "NOT",
                "Logical Negation — NOT(Condition)",
                "NOT( TRUE/FALSE )",
                "Inverts a boolean condition: makes anything TRUE become FALSE, and anything FALSE become TRUE.",
                "NOT( TRUE )  →  FALSE"),
            ["AND"] = new(
                "AND",
                "Logical Conjunction — AND(Condition1, Condition2, ...)",
                "AND( TRUE/FALSE, TRUE/FALSE, … )",
                "Evaluates multiple conditions and returns TRUE only if ALL arguments are TRUE. If even one argument is FALSE, it returns FALSE.",
                "AND( F72 = 'N', F69 < 100 )  →  TRUE when both are true"),
            ["OR"] = new(
                "OR",
                "Logical Disjunction — OR(Condition1, Condition2, ...)",
                "OR( TRUE/FALSE, TRUE/FALSE, … )",
                "Evaluates multiple conditions and returns TRUE if ANY argument is TRUE. Only returns FALSE when every argument is FALSE.",
                "OR( F72 = 'U', F72 = 'D' )  →  TRUE if Used or Demo"),
            ["ADDDAYS"] = new(
                "ADDDAYS",
                "Add Days to Date — ADDDAYS(Date, WholeNumber)",
                "ADDDAYS( Date, WholeNumber )",
                "Adds the specified whole number of days to the specified date and returns the resulting date.",
                "ADDDAYS( TODAY(), 30 )  →  30 days from today"),
            ["ADDWEEKS"] = new(
                "ADDWEEKS",
                "Add Weeks to Date — ADDWEEKS(Date, WholeNumber)",
                "ADDWEEKS( Date, WholeNumber )",
                "Adds the specified whole number of 7-day weeks to the specified date and returns the resulting date.",
                "ADDWEEKS( TODAY(), 2 )  →  14 days from today"),
            ["ADDMONTHS"] = new(
                "ADDMONTHS",
                "Add Months to Date — ADDMONTHS(Date, WholeNumber)",
                "ADDMONTHS( Date, WholeNumber )",
                "Adds the specified whole number of calendar months to the specified date and returns the resulting date.",
                "ADDMONTHS( TODAY(), 1 )  →  1 month from today"),
            ["ADDYEARS"] = new(
                "ADDYEARS",
                "Add Years to Date — ADDYEARS(Date, WholeNumber)",
                "ADDYEARS( Date, WholeNumber )",
                "Adds the specified whole number of calendar years to the specified date and returns the resulting date.",
                "ADDYEARS( TODAY(), 1 )  →  1 year from today"),
            ["MONTH"] = new(
                "MONTH",
                "Extract Month Number — MONTH(Date)",
                "MONTH( Date )",
                "Extracts the numeric month code (1 = January through 12 = December) from a Date value.",
                "MONTH( TODAY() )  →  10",
                "IMPORTANT: In FormGen, MONTH() only works directly with TODAY(), DATE(...), or Date Prompts! Standard deal date F-fields (like F44 Sale Date) are stored as text strings, or already have dedicated month fields (like F46 Sale Date Month)."),
            ["DAY"] = new(
                "DAY",
                "Extract Day of Month — DAY(Date)",
                "DAY( Date )",
                "Extracts the numeric day of the month (1–31) from a Date value.",
                "DAY( TODAY() )  →  18",
                "IMPORTANT: In FormGen, DAY() only works directly with TODAY(), DATE(...), or Date Prompts! For a deal date F-field, use its dedicated Day field (like F47 Sale Date Day) or wrap with DATE()."),
            ["YEAR"] = new(
                "YEAR",
                "Extract 4-Digit Year — YEAR(Date)",
                "YEAR( Date )",
                "Extracts the 4-digit calendar year (e.g., 2026) from a Date value. Combine with Modulus (% 100) to get a 2-digit year.",
                "YEAR( TODAY() )  →  2026   |   YEAR( TODAY() ) % 100  →  26",
                "IMPORTANT: In FormGen, YEAR() only works directly with TODAY(), DATE(...), or Date Prompts! For a deal date F-field, use its dedicated Year field (like F45 Sale Date Year)."),
            ["DATE"] = new(
                "DATE",
                "Convert Text to Date — DATE(Text)",
                "DATE( Text )",
                "Converts a text string formatted like a date (e.g., '01/16/2026') into an internal Date value.",
                "DAY( DATE( F44 ) )",
                "On its own, DATE() is functionally useless in FormGen output, but it can be nested inside DAY(DATE(...)), MONTH(DATE(...)), or YEAR(DATE(...)) when extracting date parts from text."),
            ["TEXT"] = new(
                "TEXT",
                "Convert to Text String — TEXT(NumberOrDate)",
                "TEXT( Number or Date )",
                "Converts a numeric value (or date) into a text string. In FormGen, numbers are right-justified by default; wrapping ROUND() inside TEXT(ROUND(Number, Decimals)) converts it to text so it can be left-justified or concatenated with strings!",
                "TEXT( ROUND( F3, 2 ) )  →  '25000.00' (left-justifiable text)",
                "Note: Functionally useless on a raw date by itself, and always pair with ROUND() when converting numbers because NUM() or unrounded floats can produce strange formatting in FormGen."),
            ["NUM"] = new(
                "NUM",
                "Convert Text/Date to Number — NUM(TextOrDate)",
                "NUM( Text or Date )",
                "Converts a text string or date into a numeric value if possible. Useful when you need to perform math on an alphanumeric field that contains digits, or to extract numeric components from non-prompt dates.",
                "NUM( F61 ) + 1",
                "Warning: Using NUM() alone to format numbers for display produces strange results in FormGen — use TEXT(ROUND(Number, Decimals)) instead."),
            ["IF"] = new(
                "IF",
                "Conditional Branch — IF(Condition, ResultIfTrue, ResultIfFalse)",
                "IF( Condition, ResultIfTrue, ResultIfFalse )",
                "Evaluates Argument 1 (Condition). If the condition is TRUE, it returns Argument 2 (ResultIfTrue); if the condition is FALSE, it returns Argument 3 (ResultIfFalse).",
                "IF( F72 = 'N', 'New Vehicle', 'Pre-Owned' )"),
            ["XIF"] = new(
                "XIF",
                "Checkbox Single-X Conditional — XIF(Condition)",
                "XIF( TRUE/FALSE )",
                "Returns a capital 'X' if the condition is TRUE, and outputs nothing (blank) if the condition is FALSE. Designed specifically for marking checkbox fields on FormGen forms.",
                "XIF( F72 = 'N' )  →  'X' when New, blank otherwise"),
            ["XXIF"] = new(
                "XXIF",
                "Checkbox Double-XX Conditional — XXIF(Condition)",
                "XXIF( TRUE/FALSE )",
                "Returns 'XX' if the condition is TRUE, and outputs nothing (blank) if the condition is FALSE. Used on forms where checkbox fields require a double 'XX' mark.",
                "XXIF( F436 = 'Y' )  →  'XX' when Air Conditioning is Yes"),
            ["CONTAINS"] = new(
                "CONTAINS",
                "Substring Search — CONTAINS(SearchInText, FindSubstring)",
                "CONTAINS( TextToSearchIn, SubstringToFind )",
                "Checks whether Argument 2 (SubstringToFind) appears anywhere inside Argument 1 (TextToSearchIn). Returns TRUE if found, or FALSE if not.",
                "CONTAINS( F776, 'GAS' )  →  TRUE if F776 contains 'GAS'",
                "CONTAINS is case-sensitive in FormGen, and literal search text in Argument 2 must be wrapped in single quotes (e.g. 'GAS')."),
            ["STRIP"] = new(
                "STRIP",
                "Strip Substring — STRIP(SourceText, TextToRemove)",
                "STRIP( SourceText, TextToRemove )",
                "Removes all occurrences of Argument 2 (TextToRemove) from Argument 1 (SourceText).",
                "STRIP( 'Hello World!', 'orl' )  →  'Hello Wd!'"),
            ["REPLACE"] = new(
                "REPLACE",
                "Replace Substring — REPLACE(SourceText, UnwantedText, ReplacementText)",
                "REPLACE( SourceText, UnwantedText, ReplacementText )",
                "Finds all occurrences of Argument 2 (UnwantedText) inside Argument 1 (SourceText) and replaces them with Argument 3 (ReplacementText).",
                "REPLACE( 'Hello World!', 'll', 'rr' )  →  'Herro World!'"),
            ["SEPLIST"] = new(
                "SEPLIST",
                "Separated List — SEPLIST(Delimiter, Item1, Item2, ...)",
                "SEPLIST( Delimiter, Part1, Part2, …PartN )",
                "Joins Arguments 2 and beyond together into a single text string, placing Argument 1 (Delimiter) between each non-empty item. Any blank/empty items are automatically skipped so you don't get double delimiters.",
                "SEPLIST( ' ', SEPLIST( ', ', F925, F926 ), F927 )  →  'Dallas, TX 75201'",
                "1) The Delimiter (Argument 1) must be wrapped in single quotes (e.g. ', ' or ' '). 2) Numeric fields do not cooperate directly with SEPLIST — wrap numeric fields in 'Seplist Number' (IF(Field != 0, ROUND(Field, Decimals), ''))."),
            ["CASE"] = new(
                "CASE",
                "Case Statement (Exact Match) — CASE(Comparison, Case1, Result1, ..., Default)",
                "CASE( ValueToCompare, Test1, Result1, Test2, Result2, …, DefaultResult )",
                "A compact alternative to nested IF statements. Argument 1 is the value being tested. Arguments 2..N-1 are pairs of (CaseValue, ResultIfEqual). The final argument is the fallback DefaultResult returned when none of the CaseValues match.",
                "CASE( F46, 1, 'JANUARY', 2, 'FEBRUARY', 'Other Month' )"),
            ["CASELTE"] = new(
                "CASELTE",
                "Case Less-Than-Or-Equal — CASELTE(Number, Threshold1, Result1, ..., Default)",
                "CASELTE( NumberToCompare, Max1, Result1, Max2, Result2, …, DefaultResult )",
                "Works like CASE(), except instead of testing for exact equality (=), each pair matches when Argument 1 is LESS THAN OR EQUAL TO (<=) the threshold number.",
                "CASELTE( 13, 10, '1-10', 20, '11-20', 30, '21-30', 'other' )  →  '11-20'"),
            ["MATCHRANGE"] = new(
                "MATCHRANGE",
                "Match Numeric Range — MATCHRANGE(Number, Low1, High1, Result1, ..., Default)",
                "MATCHRANGE( NumberToCompare, LowerBound1, UpperBound1, Result1, LowerBound2, UpperBound2, Result2, …, Default )",
                "Tests a number against inclusive numeric ranges [LowerBound, UpperBound]. Each scenario uses 3 arguments: (LowerBound, UpperBound, ResultIfInRange), followed by a final Default value.",
                "MATCHRANGE( 0, 0, 10, 5, 11, 20, 15, -1 )  →  5",
                "NUMBERS ONLY — all comparison values and bounds must be numeric."),
            ["BLANK"] = new(
                "BLANK",
                "Is Blank Check — BLANK(FieldOrPrompt)",
                "BLANK( Field or Prompt )",
                "Returns TRUE if the specified field or prompt is blank/empty, and FALSE if it contains a value.",
                "IF( BLANK( F292 ), F91, F292 )"),
            ["NOTBLANK"] = new(
                "NOTBLANK",
                "Is Not Blank Check — NOTBLANK(FieldOrPrompt)",
                "NOTBLANK( Field or Prompt )",
                "The exact opposite of BLANK(): returns TRUE if the specified field or prompt contains a value, and FALSE if it is empty.",
                "XIF( NOTBLANK( F477 ) )"),
            ["LEN"] = new(
                "LEN",
                "Character Length — LEN(Value)",
                "LEN( Text, Number, or Date )",
                "Counts and returns the total number of characters in the argument.",
                "LEN( '123456789' )  →  9"),
            ["TRIM"] = new(
                "TRIM",
                "Trim Leading & Trailing Spaces — TRIM(Text)",
                "TRIM( Text or Number )",
                "Removes both leading and trailing spaces from the argument.",
                "TRIM( '   20 spaces   ' )  →  '20 spaces'",
                "FormGen automatically trims leading and trailing spaces on fields, so TRIM(), LTRIM(), and RTRIM() are functionally redundant in FormGen."),
            ["LTRIM"] = new(
                "LTRIM",
                "Trim Leading Spaces — LTRIM(Text)",
                "LTRIM( Text or Number )",
                "Removes leading spaces from the start of the argument.",
                "LTRIM( '   Hello' )  →  'Hello'",
                "FormGen automatically trims spaces on fields, so LTRIM() is functionally redundant in FormGen."),
            ["RTRIM"] = new(
                "RTRIM",
                "Trim Trailing Spaces — RTRIM(Text)",
                "RTRIM( Text or Number )",
                "Removes trailing spaces from the end of the argument.",
                "RTRIM( 'Hello   ' )  →  'Hello'",
                "FormGen automatically trims spaces on fields, so RTRIM() is functionally redundant in FormGen."),
            ["TODAY"] = new(
                "TODAY",
                "Current Date — TODAY()",
                "TODAY()",
                "Returns today's current date. Takes no arguments, and is safe to use in both Deal forms and Vehicle Merchandising (VM) forms.",
                "TODAY()   |   MONTH( TODAY() )",
                "Must always include empty parentheses: TODAY()."),
            ["RANDOM"] = new(
                "RANDOM",
                "Random Decimal Number — RANDOM()",
                "RANDOM()",
                "Returns a random decimal number between 0 and 1 (with up to 40 decimal digits). Takes no arguments.",
                "RANDOM()  →  0.2626412757..."),
            ["ROUND"] = new(
                "ROUND",
                "Round to Decimal Places — ROUND(Number, DecimalPlaces)",
                "ROUND( Number, WholeNumberDecimals )",
                "Rounds Argument 1 (Number) to the number of decimal places specified in Argument 2 (use 2 for currency/money, or 0 for whole numbers). Uses nearest-digit rounding (1.5 rounds to 2, 1.4 rounds to 1).",
                "ROUND( 3.14159, 3 )  →  3.142   |   TEXT( ROUND( F3, 2 ) )"),
            ["FLOOR"] = new(
                "FLOOR",
                "Round Down to Whole Number — FLOOR(Number)",
                "FLOOR( Number )",
                "Always rounds the number DOWN to the previous whole integer (e.g., 1.9 rounds down to 1).",
                "FLOOR( 3.14159 )  →  3"),
            ["CEILING"] = new(
                "CEILING",
                "Round Up to Whole Number — CEILING(Number)",
                "CEILING( Number )",
                "Always rounds the number UP to the next whole integer (e.g., 3.14159 rounds up to 4).",
                "CEILING( 3.14159 )  →  4"),
            ["ABS"] = new(
                "ABS",
                "Absolute Value — ABS(Number)",
                "ABS( Number )",
                "Returns the distance of the number from 0, making negative numbers positive while keeping positive numbers positive.",
                "ABS( -3.14159 )  →  3.14159"),
            ["MAX"] = new(
                "MAX",
                "Maximum Value — MAX(Number1, Number2, ...)",
                "MAX( Number1, Number2, …NumberN )",
                "Compares all given numeric arguments and returns the largest number.",
                "MAX( 5, 4, 9, 2 )  →  9"),
            ["MIN"] = new(
                "MIN",
                "Minimum Value — MIN(Number1, Number2, ...)",
                "MIN( Number1, Number2, …NumberN )",
                "Compares all given numeric arguments and returns the smallest number.",
                "MIN( 5, 4, 0, 2 )  →  0"),
            ["SQRT"] = new(
                "SQRT",
                "Square Root — SQRT(PositiveNumber)",
                "SQRT( PositiveNumber )",
                "Returns the square root of a positive number.",
                "SQRT( 4 )  →  2",
                "Positive arguments (>= 0) only!"),
            ["LOG"] = new(
                "LOG",
                "Natural Logarithm (Base e) — LOG(Number)",
                "LOG( Number )",
                "Returns the natural logarithm (log base e) of the argument.",
                "LOG( 42 )  →  3.7376696..."),
            ["LOG10"] = new(
                "LOG10",
                "Base-10 Logarithm — LOG10(Number)",
                "LOG10( Number )",
                "Returns the base-10 logarithm of the argument (which corresponds to the power of 10 / digit magnitude of a number).",
                "LOG10( 10000000000 )  →  10"),
            ["SIN"] = new("SIN", "Sine — SIN(Number)", "SIN( Number )", "Performs the trigonometric Sine function on the argument (in radians).", "SIN( 42 )  →  -0.9165..."),
            ["COS"] = new("COS", "Cosine — COS(Number)", "COS( Number )", "Performs the trigonometric Cosine function on the argument (in radians).", "COS( 42 )  →  -0.3999..."),
            ["TAN"] = new("TAN", "Tangent — TAN(Number)", "TAN( Number )", "Performs the trigonometric Tangent function on the argument (in radians).", "TAN( 42 )  →  2.2913..."),
            ["ASIN"] = new("ASIN", "Arcsine — ASIN(Number)", "ASIN( Number )", "Performs the inverse Sine (Arcsine) function on the argument.", "ASIN( 1 )  →  1.570796...", "Argument must be between -1 and 1 inclusive."),
            ["ACOS"] = new("ACOS", "Arccosine — ACOS(Number)", "ACOS( Number )", "Performs the inverse Cosine (Arccosine) function on the argument.", "ACOS( -1 )  →  3.141592...", "Argument must be between -1 and 1 inclusive."),
            ["ATAN"] = new("ATAN", "Arctangent — ATAN(Number)", "ATAN( Number )", "Performs the inverse Tangent (Arctangent) function on the argument.", "ATAN( 42 )  →  1.54699..."),
            ["SINH"] = new("SINH", "Hyperbolic Sine — SINH(Number)", "SINH( Number )", "Performs the Hyperbolic Sine function on the argument.", "SINH( 1 )  →  1.1752..."),
            ["COSH"] = new("COSH", "Hyperbolic Cosine — COSH(Number)", "COSH( Number )", "Performs the Hyperbolic Cosine function on the argument.", "COSH( 1 )  →  1.5430..."),
            ["TANH"] = new("TANH", "Hyperbolic Tangent — TANH(Number)", "TANH( Number )", "Performs the Hyperbolic Tangent function on the argument.", "TANH( 42 )  →  1"),
            ["RAD"] = new("RAD", "Degrees to Radians — RAD(Degrees)", "RAD( Degrees )", "Converts an angle from angular degrees to radians.", "RAD( 42 )  →  0.733038..."),
            ["DEG"] = new("DEG", "Radians to Degrees — DEG(Radians)", "DEG( Radians )", "Converts an angle from radians to angular degrees.", "DEG( 42 )  →  2406.4227..."),
            ["PI"] = new("PI", "Pi Constant — PI", "PI", "Returns the mathematical constant Pi (3.141592653589793...) to 33 decimal places.", "PI  →  3.1415926535897931", "DOES NOT USE PARENTHESES — write PI, not PI()!")
        };

    // Complete Operator Documentation from Formgen Formulas.xlsm (Math Operations + Comparisons/Logic)
    public static IReadOnlyDictionary<string, FormgenOperatorDoc> Operators { get; } =
        new Dictionary<string, FormgenOperatorDoc>(StringComparer.OrdinalIgnoreCase)
        {
            ["+"] = new(
                "+",
                "Plus / Add / Concat",
                "Plus Operator (+) — Numeric Addition or Text Concatenation",
                "Left + Right",
                "When both Left and Right are numbers, adds them together (e.g., Selling Price + Taxed Fees). When either side is text (such as TEXT(ROUND(...)) + 'st'), concatenates the two strings together.",
                "1 + 1 = 2   |   TEXT(ROUND(F47, 0)) + 'th'  →  '15th'"),
            ["-"] = new(
                "-",
                "Minus / Subtract",
                "Minus Operator (-) — Numeric Subtraction",
                "Left - Right",
                "Subtracts the Right numeric operand from the Left numeric operand. Both inputs must evaluate to numbers.",
                "42 - 1 = 41   |   F3 - F2008 (Selling Price minus Rebates)"),
            ["*"] = new(
                "*",
                "Times / Multiply",
                "Times Operator (*) — Numeric Multiplication",
                "Left * Right",
                "Multiplies the Left numeric operand by the Right numeric operand. Both inputs must evaluate to numbers.",
                "3 * 14 = 42"),
            ["/"] = new(
                "/",
                "Divide",
                "Divide Operator (/) — Numeric Division",
                "Left / Right",
                "Divides the Left numeric operand by the Right numeric operand. The Right operand (divisor) must not be 0.",
                "42 / 3 = 14"),
            ["%"] = new(
                "%",
                "Modulus / Remainder",
                "Modulus Operator (%) — Division Remainder",
                "Left % Right",
                "Divides Left by Right and returns only the whole-number REMAINDER left over after division. Extremely useful in FormGen for extracting the last 2 digits of a year (% 100) or the ones digit of a day (% 10).",
                "43 % 5 = 3   |   YEAR(TODAY()) % 100 = 26   |   21 % 10 = 1"),
            ["e"] = new(
                "e",
                "Scientific Notation",
                "Scientific Notation Operator (e) — Multiply by Power of 10",
                "Mantissa e Exponent",
                "Multiplies the Left number by 10 raised to the Right power (Left * 10^Right). Useful for compactly writing large divisors (1e6 = 1,000,000) or decimal multipliers (1e-2 = 0.01).",
                "1e-2 = 0.01   |   1e6 = 1000000"),
            ["="] = new(
                "=",
                "Equals / Assign",
                "Equals Operator (=) — Equality Test or Variable Assignment",
                "Left = Right",
                "Inside a condition (like IF or CASE), tests whether Left and Right are equal and returns TRUE or FALSE. At the top level of an Init screen line (like FUELTYPE = IF(...)), assigns the Right result into the Left variable/prompt.",
                "F72 = 'N'  →  TRUE if New   |   FUELTYPE = 'Gas' (Assignment)"),
            ["!="] = new(
                "!=",
                "Not Equal",
                "Not Equal Operator (!=) — Inequality Comparison",
                "Left != Right",
                "Tests whether Left and Right are NOT equal: returns TRUE when they differ, and FALSE when they are equal. Commonly used in 'Seplist Number' (IF(Field != 0, ROUND(Field, 2), '')) to omit zero amounts.",
                "F123 != 0  →  TRUE when F123 is non-zero"),
            ["<"] = new(
                "<",
                "Less Than",
                "Less Than Operator (<) — Numeric Comparison",
                "Left < Right",
                "Returns TRUE if the Left number is strictly less than (<) the Right number, and FALSE otherwise.",
                "Number < 10  →  TRUE for single-digit numbers (0–9)"),
            [">"] = new(
                ">",
                "Greater Than",
                "Greater Than Operator (>) — Numeric Comparison",
                "Left > Right",
                "Returns TRUE if the Left number is strictly greater than (>) the Right number, and FALSE otherwise.",
                "F3 > 0  →  TRUE when Selling Price is positive"),
            ["<="] = new(
                "<=",
                "Less or Equal",
                "Less Than or Equal Operator (<=) — Numeric Comparison",
                "Left <= Right",
                "Returns TRUE if the Left number is less than or equal to (<=) the Right number.",
                "F69 <= 36000  →  TRUE when miles are within 36,000"),
            [">="] = new(
                ">=",
                "Greater or Equal",
                "Greater Than or Equal Operator (>=) — Numeric Comparison",
                "Left >= Right",
                "Returns TRUE if the Left number is greater than or equal to (>=) the Right number.",
                "F61 >= 2020  →  TRUE for model year 2020 or newer"),
            ["AND"] = new(
                "AND",
                "Logical AND",
                "Infix Logical AND — Both Conditions Must Be True",
                "Condition1 AND Condition2",
                "Returns TRUE only if both the Left condition and the Right condition evaluate to TRUE.",
                "(F72 = 'U') AND (F69 < 50000)"),
            ["&&"] = new(
                "&&",
                "Logical AND",
                "Infix Logical AND (&&) — Both Conditions Must Be True",
                "Condition1 && Condition2",
                "Returns TRUE only if both the Left condition and the Right condition evaluate to TRUE.",
                "(F72 = 'U') && (F69 < 50000)"),
            ["OR"] = new(
                "OR",
                "Logical OR",
                "Infix Logical OR — Either Condition Can Be True",
                "Condition1 OR Condition2",
                "Returns TRUE if either the Left condition or the Right condition (or both) evaluates to TRUE.",
                "(F72 = 'N') OR (F72 = 'D')"),
            ["||"] = new(
                "||",
                "Logical OR",
                "Infix Logical OR (||) — Either Condition Can Be True",
                "Condition1 || Condition2",
                "Returns TRUE if either the Left condition or the Right condition (or both) evaluates to TRUE.",
                "(F72 = 'N') || (F72 = 'D')")
        };

    public static FormgenFieldInfo? TryGetField(string? tokenOrNumber)
    {
        if (string.IsNullOrWhiteSpace(tokenOrNumber)) return null;
        var trimmed = tokenOrNumber.Trim();
        if (trimmed.StartsWith('F') || trimmed.StartsWith('f'))
        {
            trimmed = trimmed[1..];
        }
        if (int.TryParse(trimmed, out int fieldNum) && Fields.TryGetValue(fieldNum, out var info))
        {
            return info;
        }
        return null;
    }

    public static FormgenFieldInfo? TryGetField(int fieldNum) =>
        Fields.TryGetValue(fieldNum, out var info) ? info : null;

    public static FormgenFunctionDoc? TryGetFunctionDoc(string? prefix) =>
        !string.IsNullOrWhiteSpace(prefix) && Functions.TryGetValue(prefix.Trim(), out var doc) ? doc : null;

    public static FormgenOperatorDoc? TryGetOperatorDoc(string? op) =>
        !string.IsNullOrWhiteSpace(op) && Operators.TryGetValue(op.Trim(), out var doc) ? doc : null;

    public static bool IsPredefinedSmartField(string? name) =>
        !string.IsNullOrWhiteSpace(name) && PredefinedSmartFields.ContainsKey(name.Trim());

    public static bool TryGetSmartField(string? name, out FormgenSmartFieldInfo? info)
    {
        info = null;
        if (string.IsNullOrWhiteSpace(name)) return false;
        return PredefinedSmartFields.TryGetValue(name.Trim(), out info);
    }

    public static IReadOnlyDictionary<string, FormgenSmartFieldInfo> PredefinedSmartFields { get; } =
        BuildPredefinedSmartFields();

    private static IReadOnlyDictionary<string, FormgenSmartFieldInfo> BuildPredefinedSmartFields()
    {
        var dict = new Dictionary<string, FormgenSmartFieldInfo>(StringComparer.OrdinalIgnoreCase);

        void AddVar(string name, string category, string description, string? defaultFormat = null)
        {
            if (!dict.ContainsKey(name))
            {
                dict[name] = new FormgenSmartFieldInfo(name, category, description, defaultFormat);
            }
        }

        // 1. Buyer, CoBuyer, Reg, and CoReg Predefined Variables (from FormGen BuyerCobuyerField)
        var buyerBases = new (string BaseName, string Desc, string Format)[]
        {
            ("Name", "Full Name", "ALPHA80"),
            ("NameStacked", "Full Name (Stacked format)", "ALPHA80"),
            ("LastFirstStacked", "Last, First (Stacked format)", "ALPHA80"),
            ("FirstStacked", "First Name (Stacked format)", "ALPHA35"),
            ("MiddleStacked", "Middle Initial / Name (Stacked format)", "ALPHA1"),
            ("LastStacked", "Last Name (Stacked format)", "ALPHA50"),
            ("Addr", "Street Address", "ALPHA35"),
            ("City", "City", "ALPHA21"),
            ("St", "State Abbreviation", "ALPHA2"),
            ("Zip", "ZIP Code", "ALPHA5"),
            ("First", "First Name", "ALPHA35"),
            ("Middle", "Middle Initial / Name", "ALPHA1"),
            ("Last", "Last Name", "ALPHA50"),
            ("LastFirst", "Last Name, First Name", "ALPHA50"),
            ("DOB", "Date of Birth", "DATE"),
            ("SSN", "Social Security / Tax ID Number", "SSN"),
            ("Age", "Age in Years", "NUMERIC"),
            ("DriversLic", "Driver's License Number", "ALPHA25"),
            ("DriversLicSt", "Driver's License State", "ALPHA2"),
            ("DriversLicExp", "Driver's License Expiration Date", "DATE"),
            ("Email", "Email Address", "ALPHA60"),
            ("County", "County", "ALPHA25"),
            ("Phone", "Phone Number", "PHONE"),
            ("Gender", "Gender Code", "ALPHA1")
        };

        var prefixes = new (string Prefix, string Role)[]
        {
            ("Buyer", "Buyer"),
            ("Cobuyer", "Co-Buyer"),
            ("Reg", "Registered Owner"),
            ("CoReg", "Co-Registered Owner")
        };

        foreach (var (pfx, role) in prefixes)
        {
            foreach (var (baseName, desc, format) in buyerBases)
            {
                AddVar($"{pfx}{baseName}", "Buyer / Co-Buyer", $"{role} {desc}", format);
            }
        }

        // Combined City/State/ZIP and Co-Buyer presence flags
        AddVar("BUYERCITYSTATEZIP", "Buyer / Co-Buyer", "Buyer combined City, State, and ZIP", "BUYERCSZ");
        AddVar("COBUYERCITYSTATEZIP", "Buyer / Co-Buyer", "Co-Buyer combined City, State, and ZIP", "BUYERCSZ");
        AddVar("REGCITYSTATEZIP", "Buyer / Co-Buyer", "Registered Owner combined City, State, and ZIP", "BUYERCSZ");
        AddVar("COREGCITYSTATEZIP", "Buyer / Co-Buyer", "Co-Registered Owner combined City, State, and ZIP", "BUYERCSZ");
        AddVar("HASCOBUYER", "Buyer / Co-Buyer", "Flag indicating whether deal has an active Co-Buyer (TRUE/FALSE)", "BOOLEAN");

        // 2. Accessory Predefined Variables (ACC01..ACC15 from FormGen AccessoryVar)
        var accFields = new (string Suffix, string Desc, string Format)[]
        {
            ("RESERVE", "Reserve amount", "standardNumeric"),
            ("COST", "Cost amount", "standardNumeric"),
            ("FINLSECAP_DESC", "Finance / Lease capitalized description", "ALPHA40"),
            ("FINLSECAP_PRICE", "Finance / Lease capitalized price", "standardNumeric"),
            ("LSEINCEP_DESC", "Lease inception description", "ALPHA40"),
            ("LSEINCEP_PRICE", "Lease inception price", "standardNumeric"),
            ("LSE_CICODE", "Lease capitalize / inception indicator code", "ALPHA1"),
            ("POSDESC", "Positional description", "ALPHA40"),
            ("POSPRICE", "Positional retail price", "standardNumeric"),
            ("RESIDUAL", "Residual value amount", "standardNumeric"),
            ("RESIDUALIZE_YN", "Residualize flag (Y/N)", "ALPHA1"),
            ("TAX", "Tax amount", "standardNumeric"),
            ("TAXRATE", "Tax rate percentage", "standardNumeric"),
            ("TAXYN", "Taxable flag (Y/N)", "ALPHA1"),
            ("SPXCOMM", "Salesperson X commission amount", "standardNumeric"),
            ("SPXPCT", "Salesperson X commission percentage", "standardNumeric"),
            ("SP1COMM", "Salesperson 1 commission amount", "standardNumeric"),
            ("SP1PCT", "Salesperson 1 commission percentage", "standardNumeric"),
            ("SP2COMM", "Salesperson 2 commission amount", "standardNumeric"),
            ("SP2PCT", "Salesperson 2 commission percentage", "standardNumeric"),
            ("SP3COMM", "Salesperson 3 commission amount", "standardNumeric"),
            ("SP3PCT", "Salesperson 3 commission percentage", "standardNumeric"),
            ("SM1COMM", "Sales Manager 1 commission amount", "standardNumeric"),
            ("SM1PCT", "Sales Manager 1 commission percentage", "standardNumeric"),
            ("SM2COMM", "Sales Manager 2 commission amount", "standardNumeric"),
            ("SM2PCT", "Sales Manager 2 commission percentage", "standardNumeric"),
            ("FM1COMM", "Finance Manager 1 commission amount", "standardNumeric"),
            ("FM1PCT", "Finance Manager 1 commission percentage", "standardNumeric"),
            ("COMPNAME", "Vendor / Company name", "ALPHA40")
        };

        for (int i = 1; i <= 15; i++)
        {
            string idx = i.ToString("D2");
            foreach (var (suffix, desc, format) in accFields)
            {
                AddVar($"ACC{idx}_{suffix}", "Accessory", $"Accessory {i} {desc}", format);
            }
            AddVar($"HASACC{idx}", "Accessory", $"Flag indicating whether Accessory {i} is selected on deal", "BOOLEAN");
        }
        AddVar("ACCESSORIES_CHOSEN", "Accessory", "Formatted block of all accessories selected for the deal", "ALPHA80");

        // 3. Other Predefined Variables (from FormGen PredefinedVariable - only working underscored or formatted names)
        AddVar("_DEALNUM", "Deal / General", "Deal Number (COBOL field F1)", "INTEGER");
        AddVar("_BUYER_NAME_COBOL_FORMAT", "Buyer / Co-Buyer", "Buyer full name in raw COBOL format (F80)", "ALPHA80");
        AddVar("_REBATE_AMOUNT", "Financial", "Total rebate amount applied to deal (F229)", "standardNumeric");
        AddVar("_SALEDATE", "Deal / General", "Deal sale date (F456)", "DATE");
        AddVar("_SUBCOMPANY", "Deal / General", "Dealership sub-company code (F2887)", "INTEGER");
        AddVar("TOTALREBATEDESC", "Financial", "Total rebate description (evaluates IF(F229=0,'','REBATE'))", "ALPHA6");
        AddVar("BUYERNAMES", "Buyer / Co-Buyer", "Buyer and Co-Buyer combined names (First Middle Last)", "ALPHA80");
        AddVar("BUYERNAMES_LFM", "Buyer / Co-Buyer", "Buyer and Co-Buyer combined names (Last, First Middle)", "ALPHA80");
        AddVar("REGNAMES", "Buyer / Co-Buyer", "Registered and Co-Registered owner combined names (First Middle Last)", "ALPHA80");
        AddVar("REGNAMES_LFM", "Buyer / Co-Buyer", "Registered and Co-Registered owner combined names (Last, First Middle)", "ALPHA80");

        return dict;
    }

    private static IReadOnlyDictionary<int, FormgenFieldInfo> LoadMasterFields()
    {
        var dict = new Dictionary<int, FormgenFieldInfo>();

        try
        {
            Stream? stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("AMFormsCST.Core.Resources.fields.json");

            if (stream != null)
            {
                using (stream)
                {
                    var rawFields = JsonSerializer.Deserialize<List<RawJsonField>>(stream);
                    if (rawFields != null)
                    {
                        foreach (var rf in rawFields)
                        {
                            if (rf.FieldNum <= 0) continue;
                            bool hasVm = VmFieldSubset.TryGetValue(rf.FieldNum, out var vmEntry);
                            bool isVmSafe = hasVm && vmEntry.IsSafe;
                            bool isExcludedVm = hasVm && !vmEntry.IsSafe;

                            dict[rf.FieldNum] = new FormgenFieldInfo(
                                rf.FieldNum,
                                rf.FieldVariable ?? string.Empty,
                                rf.FieldDescription ?? $"Field {rf.FieldNum}",
                                rf.Category ?? string.Empty,
                                rf.ScreenId ?? string.Empty,
                                rf.CobolPicture ?? string.Empty,
                                rf.DefaultChar ?? string.Empty,
                                rf.LeftSize,
                                rf.RightSize,
                                isVmSafe,
                                hasVm ? vmEntry.VmDesc : null,
                                isExcludedVm);
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback to VM subset if stream cannot be read
        }

        // Ensure all VM subset fields exist even if any were missing in fields.json
        foreach (var (fieldNum, vmEntry) in VmFieldSubset)
        {
            if (!dict.ContainsKey(fieldNum))
            {
                dict[fieldNum] = new FormgenFieldInfo(
                    fieldNum,
                    vmEntry.VmVar,
                    vmEntry.VmDesc,
                    "VEHICLE MERCHANDISING",
                    "VM",
                    vmEntry.VmVar.Contains("DATE") ? "X(8)" : "X(20)",
                    string.Empty,
                    20,
                    0,
                    vmEntry.IsSafe,
                    vmEntry.VmDesc,
                    !vmEntry.IsSafe);
            }
        }

        return dict;
    }
}
