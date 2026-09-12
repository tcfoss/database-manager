namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatTransactionSignalTests
{
    [Fact]
    public void Commit_Simple()
    {
        var text = "COMMIT";
        var formatted = """
        COMMIT;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Commit_WithWork()
    {
        var text = "COMMIT WORK";
        var formatted = """
        COMMIT WORK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Commit_AndChain()
    {
        var text = "COMMIT AND CHAIN";
        var formatted = """
        COMMIT AND CHAIN;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Commit_Release()
    {
        var text = "COMMIT RELEASE";
        var formatted = """
        COMMIT RELEASE;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Rollback_Simple()
    {
        var text = "ROLLBACK";
        var formatted = """
        ROLLBACK;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Rollback_WorkAndNoChain()
    {
        var text = "ROLLBACK WORK AND NO CHAIN";
        var formatted = """
        ROLLBACK WORK AND NO CHAIN;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void StartTransaction_Simple()
    {
        var text = "START TRANSACTION";
        var formatted = """
        START TRANSACTION;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void StartTransaction_ReadOnly()
    {
        var text = "START TRANSACTION READ ONLY";
        var formatted = """
        START TRANSACTION READ ONLY;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Signal_SqlState()
    {
        var text = "SIGNAL SQLSTATE '45000'";
        var formatted = """
        SIGNAL SQLSTATE '45000';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Signal_SqlStateWithInformationItem()
    {
        var text = "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'error occurred'";
        var formatted = """
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'error occurred';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Resignal_Simple()
    {
        var text = "RESIGNAL";
        var formatted = """
        RESIGNAL;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Resignal_SqlState()
    {
        var text = "RESIGNAL SQLSTATE '45000'";
        var formatted = """
        RESIGNAL SQLSTATE '45000';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Resignal_WithInformationItem()
    {
        var text = "RESIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'error occurred'";
        var formatted = """
        RESIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'error occurred';
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
