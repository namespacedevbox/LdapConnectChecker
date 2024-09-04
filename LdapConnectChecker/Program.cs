using LdapConnectChecker.Models;
using Microsoft.Extensions.Configuration;
using Novell.Directory.Ldap;
using Novell.Directory.Ldap.Controls;
using Novell.Directory.Ldap.SearchExtensions;

namespace LdapConnectChecker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(".net6 v1.1");

                var configuration = new ConfigurationBuilder()
                   .SetBasePath(AppContext.BaseDirectory)
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables()
                   .Build();

                Console.WriteLine("Loading credentials.");

                var credentials = new Credentials();
                configuration.GetSection("Credentials").Bind(credentials);

                if (string.IsNullOrEmpty(credentials.Domain) || string.IsNullOrEmpty(credentials.UserName) || string.IsNullOrEmpty(credentials.Password))
                {
                    Console.WriteLine($"Credentials not set.");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Connecting to '{credentials.Domain}'.");

                var ldapConnectionOptions = new LdapConnectionOptions()
                    .UseSsl()
                    .ConfigureRemoteCertificateValidationCallback((sender, certificate, chain, errors) => true);

                using var connection = new LdapConnection(ldapConnectionOptions);
           
                connection.Connect(credentials.Domain, LdapConnection.DefaultSslPort);
                connection.Bind(LdapConnection.LdapV3, credentials.UserName, credentials.Password);

                var dn = GetDnFromHost(credentials.Domain);
                var groupFilter = $"(&(objectCategory=group)(name={credentials.SyncGroupName}))";
                var groupResponse = connection.Search(dn, LdapConnection.ScopeSub, groupFilter, null, false);
       
                Console.WriteLine($"Search group '{credentials.SyncGroupName}'.");

                var groups = GetEntries(groupResponse);
                if (groups.Count == 0)
                {
                    Console.WriteLine($"Sync group '{credentials.SyncGroupName}' not found.");
                    Console.ReadKey();
                    return;
                }

                try
                {
                    Console.WriteLine("Getting users...");

                    var groupDn = groups.FirstOrDefault().Dn;
                    var membersFilter = $"(&(objectCategory=user)(memberOf={groupDn}))";
                    var searchOptions = new SearchOptions(dn, LdapConnection.ScopeSub, membersFilter, null);
                    var ldapSortControl = new LdapSortControl(new LdapSortKey("cn"), true);
                    var members = connection.SearchUsingVlv(ldapSortControl, searchOptions, 1000);

                    Console.WriteLine($"Users conut: {members.Count}");
                }
                catch (LdapException ex)
                {
                    throw new Exception($"{ex.Message}. {ex.LdapErrorMessage}");
                }

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}.");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.ReadKey();
            }
        }

        private static string GetDnFromHost(string hostname)
        {
            char separator = '.';
            var parts = hostname.Split(separator);
            var dnParts = parts.Select(_ => $"dc={_}");
            return string.Join(",", dnParts);
        }

        private static List<LdapEntry> GetEntries(ILdapSearchResults searchResult)
        {
            var entries = new List<LdapEntry>();

            try
            {
                while (searchResult.HasMore())
                {
                    entries.Add(searchResult.Next());
                }
            }
            catch (LdapException ex)
            {
                Console.WriteLine($"LdapException in SearchResult.HasMore: {ex.Message}");
            }

            return entries;
        }
    }
}