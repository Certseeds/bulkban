namespace bulkban.Wdac;

internal interface IPowerShellHost
{
    bool IsConfigCiAvailable();

    void AddDenySigner(string xmlPath, string certPath);

    void ConvertToCip(string xmlPath, string cipPath);
}
