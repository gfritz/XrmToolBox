using Microsoft.Xrm.Sdk;
using System.Collections.Generic;

namespace MsCrmTools.UserSettingsUtility.AppCode
{
    internal class UserSettings
    {
        public int AdvancedFindStartupMode { get; set; }
        public int AutoCreateContactOnPromote { get; set; }
        public EntityReference Currency { get; set; }
        public int DefaultCalendarView { get; set; }
        public int? HelpLanguage { get; set; }
        public string HomePageArea { get; set; }
        public string HomePageSubArea { get; set; }
        public int IncomingEmailFilteringMethod { get; set; }
        public bool? IsSendAsAllowed { get; set; }
        public int? PagingLimit { get; set; }
        public int ReportScriptErrors { get; set; }
        public bool? StartupPaneEnabled { get; set; }
        public int? TimeZoneCode { get; set; }
        public int? UiLanguage { get; set; }
        public bool? UseCrmFormForAppointment { get; set; }
        public bool? UseCrmFormForContact { get; set; }
        public bool? UseCrmFormForEmail { get; set; }
        public bool? UseCrmFormForTask { get; set; }
        public List<Entity> UsersToUpdate { get; set; }
        public string WorkdayStartTime { get; set; }

        public string WorkdayStopTime { get; set; }
        public UserSettingsFormatting FormatCodes { get; set; }
    }

    internal class UserSettingsFormatting
    {
        NumberFormatting NumberFormatting { get; set; }
        CurrencyFormatting CurrencyFormatting { get; set; }
        TimeFormatting TimeFormatting { get; set; }
        DateFormatting DateFormatting { get; set; }
    }

    enum DecimalSymbol { }
    enum DigitGroupingSybmol { }
    enum DigitGroups { }
    enum NegativeNumbers { }

    internal class NumberFormatting
    {
        DecimalSymbol DecimalSymbol;
        DigitGroupingSybmol DigitGroupingSymbol;
        DigitGroups DigitGroups;
        NegativeNumbers NegativeNumbers;
    }

    enum CurrencyFormat { }
    enum NegativeCurrencyFormat { }

    internal class CurrencyFormatting
    {
        CurrencyFormat CurrencyFormat;
        NegativeCurrencyFormat NegativeCurrencyFormat;
    }

    enum TimeFormat { }
    enum TimeSeparator { }
    enum AMSymbol { }
    enum PMSymbol { }

    internal class TimeFormatting
    {
        TimeFormat TimeFormat;
        TimeSeparator TimeSeparator;
        AMSymbol AMSymbol;
        PMSymbol PMSymbol;
    }

    enum ShortDateFormat { }
    enum DateSeparator { }
    enum LongDateFormat { }

    internal class DateFormatting
    {
        bool ShowWeekNumbersOnCalendarViews;
        ShortDateFormat ShortDateFormat;
        DateSeparator DateSeparator;
        LongDateFormat LongDateFormat;
    }
}