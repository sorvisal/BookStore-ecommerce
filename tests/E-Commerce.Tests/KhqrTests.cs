using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using E_Commerce.Services;
using E_Commerce.Services.Khqr;
using Xunit;

namespace E_Commerce.Tests;

public class KhqrTests
{
    // Small TLV parser for verifying tags 53, 54, 58 and 59.
    private record TlvNode(string Tag, int Length, string Value);

    private sealed class TlvParser
    {
        public static Dictionary<string, TlvNode> Parse(string input)
        {
            var result = new Dictionary<string, TlvNode>(System.StringComparer.Ordinal);
            int pos = 0;
            while (pos + 2 <= input.Length)
            {
                string tag = input.Substring(pos, 2);
                if (pos + 4 > input.Length)
                {
                    break;
                }

                int length = int.Parse(input.Substring(pos + 2, 2), CultureInfo.InvariantCulture);
                if (pos + 4 + length > input.Length)
                {
                    break;
                }

                string value = input.Substring(pos + 4, length);
                result[tag] = new TlvNode(tag, length, value);
                pos += 4 + length;
            }

            return result;
        }
    }

    [Fact]
    public void Crc16CcittFalse_StandardCheckValue_Equals29B1()
    {
        Assert.Equal("29B1", KhqrBuilder.Crc16CcittFalse("123456789"));
    }

    [Fact]
    public void BuildBakongKhqr_Last4Chars_AreCrcOfRest()
    {
        var request = new BakongKhqrRequest
        {
            Amount = 12.50m,
            Currency = "USD",
            TranId = "TEST1234",
            MerchantName = "Test Merchant",
            BakongAccountId = "test@abaa",
            AbaAccountNumber = "123456789012345",
            BankName = "ABA Bank",
            PaymentLink = null,
            ExpiresInMinutes = 5,
        };

        string khqr = KhqrBuilder.BuildBakongKhqr(request);
        Assert.True(khqr.Length >= 4);
        string rest = khqr.Substring(0, khqr.Length - 4);
        string crc = khqr.Substring(khqr.Length - 4);
        Assert.Equal(crc, KhqrBuilder.Crc16CcittFalse(rest));
    }

    [Fact]
    public void TlvParser_Reads_53_54_58_59()
    {
        var request = new BakongKhqrRequest
        {
            Amount = 12.50m,
            Currency = "USD",
            TranId = "TEST5678",
            MerchantName = "Test Shop",
            BakongAccountId = "merchant@abaa",
            AbaAccountNumber = "123456789012345",
            BankName = "ABA Bank",
            PaymentLink = null,
            ExpiresInMinutes = 10,
        };

        string khqr = KhqrBuilder.BuildBakongKhqr(request);
        var nodes = TlvParser.Parse(khqr);

        Assert.True(nodes.ContainsKey("53"));
        Assert.Equal("840", nodes["53"].Value);

        Assert.True(nodes.ContainsKey("54"));
        Assert.Equal("12.5", nodes["54"].Value);

        Assert.True(nodes.ContainsKey("58"));
        Assert.Equal("KH", nodes["58"].Value);

        Assert.True(nodes.ContainsKey("59"));
        Assert.Equal("Test Shop", nodes["59"].Value);
    }

    [Fact]
    public void FormatAmount_ProducesExpectedValues()
    {
        Assert.Equal("12.5", KhqrBuilder.FormatAmount(12.50m, "USD"));
        Assert.Equal("12", KhqrBuilder.FormatAmount(12.00m, "USD"));
        Assert.Equal("50000", KhqrBuilder.FormatAmount(50000m, "KHR"));
    }

    // --- Strict ABA approval parsing (data.action == "approved" only) ---

    [Theory]
    [InlineData("{\"data\":{\"action\":\"approved\"}}")]
    [InlineData("{\"data\":{\"action\":\"Approved\"}}")]
    [InlineData("{\"data\":{\"action\":\"APPROVED\"}}")]
    [InlineData("{\"data\":{\"action\":\"  approved  \"}}")]
    public void Parse_DataActionApproved_IsApproved(string json)
    {
        PayWayStatusParseResult result = PayWayStatusParser.Parse(json);
        Assert.True(result.Approved);
        Assert.False(result.Malformed);
    }

    [Fact]
    public void Parse_PendingActionWithSuccessStatus_IsNotApproved()
    {
        const string json =
            "{\"status\":{\"code\":\"00\",\"message\":\"Success\"},\"data\":{\"action\":\"pending\"}}";

        PayWayStatusParseResult result = PayWayStatusParser.Parse(json);

        Assert.False(result.Approved);
        Assert.Equal("pending", result.Action);
        Assert.Equal("00", result.StatusCode);
        Assert.Equal("Success", result.StatusMessage);
    }

    [Theory]
    [InlineData("{\"status\":{\"code\":\"00\",\"message\":\"Success\"},\"data\":{}}")]
    [InlineData("{\"data\":{\"action\":\"completed\"}}")]
    [InlineData("{\"data\":{\"action\":\"success\"}}")]
    [InlineData("{\"data\":{\"action\":\"ok\"}}")]
    [InlineData("{\"data\":{\"action\":\"cancelled\"}}")]
    [InlineData("{\"action\":\"approved\"}")] // top-level action is NOT the approval signal
    public void Parse_NonApprovedAction_IsNotApproved(string json)
    {
        Assert.False(PayWayStatusParser.Parse(json).Approved);
    }

    [Fact]
    public void Parse_EmptyObject_IsNotApproved()
    {
        Assert.False(PayWayStatusParser.Parse("{}").Approved);
    }

    [Theory]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{\"action\":null}}")]
    [InlineData("{\"status\":{\"code\":\"00\"}}")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_MissingData_IsNotApproved(string? json)
    {
        Assert.False(PayWayStatusParser.Parse(json).Approved);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"data\":")]
    public void Parse_MalformedJson_IsNotApproved(string json)
    {
        PayWayStatusParseResult result = PayWayStatusParser.Parse(json);
        Assert.False(result.Approved);
        Assert.True(result.Malformed);
    }

    // --- Exact payment-status string helper ---

    [Theory]
    [InlineData("Paid", true)]
    [InlineData("paid", true)]
    [InlineData("  Paid ", true)]
    [InlineData("Unpaid", false)]
    [InlineData("Pending", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPaidStatus_UsesExactMatchOnly(string? status, bool expected)
    {
        Assert.Equal(expected, PaymentStatusHelper.IsPaidStatus(status));
    }

    [Theory]
    [InlineData("Paid", "Paid", true)]
    [InlineData("Paid", "Unpaid", false)]
    [InlineData("Unpaid", "Paid", false)]
    [InlineData("Paid", null, false)]
    [InlineData(null, null, false)]
    public void IsOrderPaid_RequiresBothStatusesPaid(string? orderStatus, string? payStatus, bool expected)
    {
        Assert.Equal(expected, PaymentStatusHelper.IsOrderPaid(orderStatus, payStatus));
    }
}
