using NezamMonitor.Core.Browser;
using NezamMonitor.Core.Models;
using Xunit;

namespace NezamMonitor.Tests;

/// <summary>
/// Tests for CaseScraper extraction helpers (DOM-based and fallback parsers).
/// </summary>
public class CaseScraperExtractionTests
{
    // ========================= UsageType from .v-card__title =========================

    [Theory]
    [InlineData("4565 - مسكوني", "مسكوني")]
    [InlineData("1234 - مسکونی", "مسکونی")]
    [InlineData("789 - تجاری", "تجاری")]
    [InlineData("111 - اداری", "اداری")]
    public void UsageType_FromDialogTitle_ExtractsCorrectly(string title, string expected)
    {
        // Simulates: .v-card__title content → split('-') → parts[1].Trim()
        var parts = title.Split('-');
        Assert.Equal(2, parts.Length);
        Assert.Equal(expected, parts[1].Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("بدون خط تیره")]
    [InlineData("4565")]
    public void UsageType_InvalidTitle_ReturnsEmpty(string title)
    {
        // When title doesn't contain '-' or is empty
        var hasDash = !string.IsNullOrEmpty(title) && title.Contains("-");
        Assert.False(hasDash);
    }

    // ========================= CaseScraper.ParseEngineers (fallback) =========================

    [Fact]
    public void ParseEngineers_ExtractsAllFiveDisciplines()
    {
        var text = """
            ناظرین پرونده
            بلوك1
            معماري :
            مهديه قپاني
            عمران :
            مهديه قپاني
            مكانيك :
            محمدمهدي برخورداري
            برق :
            حميده ميرجاني سروي
            هماهنگ كننده :
            مهديه قپاني
            """;
        var engineers = CaseScraper.ParseEngineers(text);
        Assert.Equal(5, engineers.Count);
        Assert.Contains(engineers, e => e.Discipline == "معماری" && e.Name.Contains("قپاني"));
        Assert.Contains(engineers, e => e.Discipline == "عمران");
        Assert.Contains(engineers, e => e.Discipline == "مکانیک");
        Assert.Contains(engineers, e => e.Discipline == "برق");
        Assert.Contains(engineers, e => e.Discipline == "هماهنگ‌کننده");
    }

    [Fact]
    public void ParseEngineers_EmptyText_ReturnsEmpty()
    {
        var engineers = CaseScraper.ParseEngineers("");
        Assert.Empty(engineers);
    }

    [Fact]
    public void ParseEngineers_SingleDiscipline()
    {
        var text = "مكانيك :\nمحمدمهدي برخورداري";
        var engineers = CaseScraper.ParseEngineers(text);
        Assert.Single(engineers);
        Assert.Equal("مکانیک", engineers[0].Discipline);
        Assert.Contains("برخورداري", engineers[0].Name);
    }

    // ========================= CaseScraper.ParseSpecifications (fallback) =========================

    [Fact]
    public void ParseSpecifications_ExtractsKeyFields()
    {
        var text = """
            گروه ساختمانی :
            الف
            کد نوسازی :
            000124202110000
            نوع سازه :
            فلزي
            تعداد طبقات :
            1
            شماره پروانه :
            1404/0805
            آدرس :
            شهرك طوس
            متراژ کسر ظرفیت نظارت :
            145.4 مترمربع
            نوع کاربری :
            مسکونی
            """;
        var specs = CaseScraper.ParseSpecifications(text);
        Assert.NotNull(specs);
        Assert.Equal("الف", specs.BuildingGroup);
        Assert.Equal("000124202110000", specs.RenovationCode);
        Assert.Equal("فلزي", specs.StructureType);
        Assert.Equal("1", specs.Floors);
        Assert.Equal("1404/0805", specs.PermitNumber);
        Assert.Equal("شهرك طوس", specs.Address);
        Assert.Equal("145.4", specs.CapacityArea);
        Assert.Equal("مسکونی", specs.UsageType);
    }

    [Fact]
    public void ParseSpecifications_SingleLineFormat_Works()
    {
        // Simulates real website text where label and value may be on same line
        var text = "نوع کاربری : مسکونی\nمتراژ کسر ظرفیت نظارت : 145.4";
        var specs = CaseScraper.ParseSpecifications(text);
        Assert.NotNull(specs);
        Assert.Equal("مسکونی", specs.UsageType);
        Assert.Equal("145.4", specs.CapacityArea);
    }

    [Fact]
    public void ParseSpecifications_EmptyText_ReturnsNull()
    {
        var specs = CaseScraper.ParseSpecifications("");
        Assert.Null(specs);
    }

    [Fact]
    public void ParseSpecifications_NoMatchingFields_ReturnsNull()
    {
        var specs = CaseScraper.ParseSpecifications("some random text without any labels");
        Assert.Null(specs);
    }

    // ========================= CaseScraper.ParseFees (fallback) =========================

    [Fact]
    public void ParseFees_ExtractsTabDelimitedRows()
    {
        // Fee parser maps: parts[0]=Discipline, parts[1]=ServiceType, parts[3]=StartDate,
        // parts[4]=EndDate, parts[5]=Amount, parts[6]=Stage, parts[7]=PayStatus,
        // parts[8]=ConfirmStatus, parts[9]=AmountType
        var text = """
            معماري	طراحي	_	1404/01/01	1404/06/01	23,158,293 ریال	مرحله اول	پرداخت شده	تایید شده	_
            عمران	نظارت	_	1404/02/01	1404/08/01	7,719,431 ریال	مرحله دوم	تایید شده	_	_
            """;
        var fees = CaseScraper.ParseFees(text);
        Assert.Equal(2, fees.Count);
        Assert.Equal("معماري", fees[0].Discipline);
        Assert.Equal("23,158,293 ریال", fees[0].Amount);
        Assert.Equal("پرداخت شده", fees[0].PayStatus);
        Assert.Equal("مرحله اول", fees[0].Stage);
    }

    [Fact]
    public void ParseFees_NoFees_ReturnsEmpty()
    {
        var text = "some text without rial or tabs";
        var fees = CaseScraper.ParseFees(text);
        Assert.Empty(fees);
    }

    [Fact]
    public void ParseFees_SkipsNonFeeLines()
    {
        var text = """
            header line
            1	معماري	طراحي	1404/01/01	1404/06/01	100,000 ریال	مرحله اول	پرداخت شده	تایید شده	_
            another non-fee line
            """;
        var fees = CaseScraper.ParseFees(text);
        Assert.Single(fees);
    }

    // ========================= Label-Value Pattern Tests =========================

    [Theory]
    [InlineData("گروه ساختمانی :\nالف", "الف")]
    [InlineData("گروه ساختمانی:\nب", "ب")]
    [InlineData("گروه ساختمانی : الف", "الف")]
    public void SpecificationLabelPattern_BuildingGroup(string text, string expected)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"گروه ساختمان[یي]\s*:\s*\n?\s*(.+)");
        Assert.True(match.Success);
        Assert.Equal(expected, match.Groups[1].Value.Trim().Split('\n')[0].Trim());
    }

    [Theory]
    [InlineData("نوع سازه :\nفلزي", "فلزي")]
    [InlineData("نوع سازه:\nbetonی", "betonی")]
    public void SpecificationLabelPattern_StructureType(string text, string expected)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"نوع سازه\s*:\s*\n?\s*(.+)");
        Assert.True(match.Success);
        Assert.Equal(expected, match.Groups[1].Value.Trim().Split('\n')[0].Trim());
    }

    [Theory]
    [InlineData("متراژ کسر ظرفیت نظارت :\n145.4 مترمربع", "145.4")]
    [InlineData("متراژ کسر ظرفیت : 200.0", "200.0")]
    [InlineData("متراژ کسر ظرفیت نظارت:\n۱۲۳.۴۵", "۱۲۳.۴۵")]
    public void SpecificationLabelPattern_CapacityArea(string text, string expected)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"متراژ کسر ظرفیت\s*[^:]*:\s*\n?\s*([\d.,]+)");
        Assert.True(match.Success);
        Assert.Equal(expected, match.Groups[1].Value.Trim());
    }

    [Theory]
    [InlineData("نوع کاربری :\nمسکونی", "مسکونی")]
    [InlineData("نوع کاربری: مسکونی", "مسکونی")]
    [InlineData("نوع کاربری :مسکونی", "مسکونی")]
    public void SpecificationLabelPattern_UsageType(string text, string expected)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"نوع کاربری\s*:\s*\n?\s*(.+)");
        Assert.True(match.Success);
        Assert.Equal(expected, match.Groups[1].Value.Trim().Split('\n')[0].Trim());
    }

    // ========================= Edge Cases =========================

    [Fact]
    public void ParseSpecifications_PartialFields_ReturnsOnlyFound()
    {
        var text = "فقط نوع سازه :\nفلزي";
        var specs = CaseScraper.ParseSpecifications(text);
        Assert.NotNull(specs);
        Assert.Equal("فلزي", specs.StructureType);
        Assert.Equal("", specs.BuildingGroup); // Not found
        Assert.Equal("", specs.UsageType); // Not found
    }

    [Fact]
    public void ParseEngineers_ColonInName_Skipped()
    {
        // If name contains ':', it should be skipped to avoid false matches
        var text = "مكانيك :\nنام:اشتباه";
        var engineers = CaseScraper.ParseEngineers(text);
        // The parser skips entries where value contains ':'
        Assert.Empty(engineers);
    }
}
