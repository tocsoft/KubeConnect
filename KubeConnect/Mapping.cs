namespace KubeConnect
{
    public class Mapping
    {
        private string v;

        public Mapping(string v)
        {
            this.v = v;
        }

        public string ServiceName { get; set; }
        public int LocalPort { get; set; }
        public int RemotePort { get; set; }
    }

}