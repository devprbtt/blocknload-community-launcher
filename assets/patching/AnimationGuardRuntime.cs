namespace BnlCommunityFixes
{
    public static class AnimationGuardRuntime
    {
        private static readonly System.Collections.Generic.HashSet<string> ReportedMissingNodes =
            new System.Collections.Generic.HashSet<string>();

        public static void PlayMovement(AnimLayer layer, string nodeName, float speed)
        {
            if (layer == null)
            {
                ReportOnce("<null layer>", -1, 0);
                return;
            }

            AnimNode node;
            if (layer.Actions == null ||
                string.IsNullOrEmpty(nodeName) ||
                !layer.Actions.TryGetValue(nodeName, out node) ||
                node == null)
            {
                ReportOnce(nodeName, layer.LayerNum, layer.Actions == null ? 0 : layer.Actions.Count);
                return;
            }

            if (!layer.IsPlaying(nodeName))
            {
                layer.Play(nodeName);
            }
            node.OnSetSpeed(speed);
        }

        private static void ReportOnce(string nodeName, int layerNumber, int availableNodes)
        {
            string safeName = string.IsNullOrEmpty(nodeName) ? "<empty>" : nodeName;
            string key = layerNumber + ":" + safeName;
            if (!ReportedMissingNodes.Add(key)) return;
            UnityEngine.Debug.LogWarning(
                "[BNL AnimationGuard] Missing movement node '" + safeName +
                "' on animation layer " + layerNumber +
                " (available nodes: " + availableNodes + "). Repeated exceptions suppressed.");
        }
    }
}
