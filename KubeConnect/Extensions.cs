using k8s.Models;

namespace KubeConnect
{
    public static class Extensions
    {
        public static bool Match(this V1ObjectMeta metadata, ServiceDetails service)
        {
            var labels = metadata.Labels;
            var selector = service.Selector;

            foreach (var sel in selector)
            {
                // label missing or value not exists
                if (!labels.TryGetValue(sel.Key, out var val) || val != sel.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool MatchTemplate(this V1Deployment deployment, ServiceDetails service)
            => Match(deployment.Spec.Template.Metadata, service);

        public static bool Match(this V1Pod pod, ServiceDetails service)
            => Match(pod.Metadata, service);
    }
}
