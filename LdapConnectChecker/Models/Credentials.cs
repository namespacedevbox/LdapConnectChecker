namespace LdapConnectChecker.Models
{
    public class Credentials
    {
        public string Domain { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string SyncGroupName { get; set; }
    }
}