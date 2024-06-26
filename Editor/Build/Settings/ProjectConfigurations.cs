using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SuperUnityBuild.BuildTool
{
    [Serializable]
    public class ProjectConfigurations
    {
        // Data
        public ConfigDictionary configSet;

        // View
        public bool showViewOptions = false;
        public bool showConfigs = false;
        public bool showBuildInfo = false;

        public bool hideDisabled = false;
        public bool treeView = false;

        public string selectedKeyChain = string.Empty;

        public void Refresh()
        {
            ConfigDictionary refreshedConfigSet = new ConfigDictionary();

            BuildReleaseType[] releaseTypes = BuildSettings.releaseTypeList.releaseTypes;
            for (int i = 0; i < releaseTypes.Length; i++)
            {
                string key = releaseTypes[i].typeName;
                Configuration relConfig = new Configuration();

                // Check for duplicate.
                if (refreshedConfigSet.ContainsKey(key))
                    continue;

                // Copy old setting if it exists.
                if (configSet != null && configSet.ContainsKey(key))
                {
                    relConfig.enabled = configSet[key].enabled;
                }

                // Get child keys.
                relConfig.childKeys = RefreshPlatforms(key, refreshedConfigSet, configSet);

                // Save configuration.
                refreshedConfigSet.Add(key, relConfig);
            }

            configSet = refreshedConfigSet;

            SanityCheck(releaseTypes);
        }

        private static void SanityCheck(BuildReleaseType[] releaseTypes)
        {
            //for each release type (e.g. Internal) (there should only be one)
            for (int i = 0; i < releaseTypes.Length; i++)
            {
                //this is just Internal or Release or whatever
                string rootKey = releaseTypes[i].typeName;
                Configuration rootConfig = BuildSettings.projectConfigurations.configSet[rootKey];

                //for each platform...
                for (int j = 0; j < rootConfig.childKeys.Length; j++)
                {
                    //we need some recursion to reach the bottom of the tree
                    string secondaryKey = rootConfig.childKeys[j];
                    Configuration secondaryConfig = BuildSettings.projectConfigurations.configSet[secondaryKey];

                    int sanity = 0;
                    while (secondaryConfig.childKeys.Length > 0)
                    {
                        secondaryKey = secondaryConfig.childKeys[0];
                        secondaryConfig = BuildSettings.projectConfigurations.configSet[secondaryKey];

                        //this must be the end of the tree
                        if (secondaryConfig.childKeys == null)
                            break;

                        sanity++;
                        if (sanity > 10)
                        {
                            Debug.LogError("baka");
                            break;
                        }
                    }

                    //if the bottom of the tree is enabled, then that's that
                    if (secondaryConfig.enabled)
                    {
                        bool error = false;

                        //the name is actually close to the root, not the bottom of the tree (that would be Mono)
                        string activePlatformName = rootConfig.childKeys[j];

                        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Switch && !activePlatformName.Contains("/Nintendo Switch"))
                        {
                            error = true;
                        }
                        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.PS5 && !activePlatformName.Contains("/PlayStation 5"))
                        {
                            error = true;
                        }
                        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64 && !activePlatformName.Contains("/Linux"))
                        {
                            error = true;
                        }
                        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 && !activePlatformName.Contains("/PC"))
                        {
                            error = true;
                        }
                        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX && !activePlatformName.Contains("/macOS"))
                        {
                            error = true;
                        }

                        //MyPrint.Log("Unity build target: " + EditorUserBuildSettings.activeBuildTarget + "\nCurrent SuperUnityBuild: " + activePlatformName);

                        if (error)
                        {
                            MyPrint.LogError("SuperUnityBuild's current target is " + activePlatformName + "\n" +
                                "Unity's build target is " + EditorUserBuildSettings.activeBuildTarget + "\n" +
                                "You must change Unity's build target to match, otherwise the addressables will fail!");
                        }
                    }
                }
            }
        }

        public string[] BuildAllKeychains()
        {
            List<string> keychains = new List<string>();

            BuildReleaseType[] releaseTypes = BuildSettings.releaseTypeList.releaseTypes;
            for (int i = 0; i < releaseTypes.Length; i++)
            {
                string key = releaseTypes[i].typeName;

                if (configSet.ContainsKey(key))
                {
                    Configuration config = configSet[key];
                    BuildKeychainsRecursive(ref keychains, config, key, 0);
                }
            }

            return keychains.ToArray();
        }

        public int GetEnabledBuildsCount()
        {
            int count = 0;

            BuildReleaseType[] releaseTypes = BuildSettings.releaseTypeList.releaseTypes;
            for (int i = 0; i < releaseTypes.Length; i++)
            {
                string key = releaseTypes[i].typeName;

                if (configSet.ContainsKey(key))
                {
                    Configuration config = configSet[key];
                    NavigateTree(key, config, 0, ref count);
                }
            }

            return count;
        }

        private void NavigateTree(string key, Configuration config, int depth, ref int count)
        {
            if (depth >= 2 && config.enabled && (config.childKeys == null || config.childKeys.Length == 0))
            {
                ++count;
            }
            else if (config.enabled && config.childKeys != null)
            {
                foreach (string childKey in config.childKeys)
                {
                    NavigateTree(childKey, configSet[childKey], depth + 1, ref count);
                }
            }
        }

        private void BuildKeychainsRecursive(ref List<string> keychains, Configuration config, string key, int depth)
        {
            if (depth >= 2 && config.enabled && (config.childKeys == null || config.childKeys.Length == 0))
            {
                keychains.Add(key);
            }
            else if (config.childKeys != null && config.childKeys.Length > 0 && config.enabled)
            {
                foreach (string childKey in config.childKeys)
                {
                    BuildKeychainsRecursive(ref keychains, configSet[childKey], childKey, depth + 1);
                }
            }
        }

        public bool ParseKeychain(string keychain, out BuildReleaseType releaseType, out BuildPlatform platform, out BuildArchitecture architecture,
            out BuildScriptingBackend scriptingBackend, out BuildDistribution distribution)
        {
            bool success = false;
            string[] keys = keychain.Split('/');
            int keyCount = keys.Length;
            int targetKey = 0;

            releaseType = null;
            platform = null;
            architecture = null;
            distribution = null;
            scriptingBackend = null;

            // Parse release type.
            if (keyCount > targetKey)
            {
                for (int i = 0; i < BuildSettings.releaseTypeList.releaseTypes.Length; i++)
                {
                    BuildReleaseType rt = BuildSettings.releaseTypeList.releaseTypes[i];

                    if (keys[targetKey] == rt.typeName)
                    {
                        releaseType = rt;
                        break;
                    }
                }
            }

            if (releaseType == null)
                return false;

            // Parse platform.
            if (keyCount > ++targetKey)
            {
                // Scan ahead and try to parse a variant key.
                string variantKey = "";
                if (keys[targetKey + 1].Contains("("))
                {
                    int startIndex = keys[targetKey + 1].IndexOf('(');
                    int endIndex = keys[targetKey + 1].IndexOf(')');
                    variantKey = keys[targetKey + 1].Substring(startIndex + 1, endIndex - startIndex - 1);

                    keys[targetKey + 1] = keys[targetKey + 1].Remove(startIndex).Trim();
                }

                for (int i = 0; i < BuildSettings.platformList.platforms.Count; i++)
                {
                    BuildPlatform p = BuildSettings.platformList.platforms[i];

                    if (keys[targetKey] == p.platformName && p.variantKey == variantKey)
                    {
                        platform = p;
                        break;
                    }
                }
            }

            if (platform == null)
                return false;

            // Parse architecture.
            if (platform.architectures.Length == 1)
            {
                // Only one architecture, so it won't even appear in dictionary. Just get it directly.
                ++targetKey;
                architecture = platform.architectures[0];
                success = true;
            }
            else if (keyCount > ++targetKey)
            {
                for (int i = 0; i < platform.architectures.Length; i++)
                {
                    BuildArchitecture arch = platform.architectures[i];

                    if (keys[targetKey] == arch.name)
                    {
                        architecture = arch;
                        success = true;
                        break;
                    }
                }
            }

            if (architecture == null)
                return false;

            //Parse scripting backend
            if (platform.scriptingBackends.Length == 0)
            {
                //If no scripting backends are available,
                //use Mono2x by default
                scriptingBackend = new BuildScriptingBackend(UnityEditor.ScriptingImplementation.Mono2x, true);
                success = true;
            }
            else if (keyCount > ++targetKey)
            {
                //Else search for existing backends
                success = false;
                for (int i = 0; i < platform.scriptingBackends.Length; i++)
                {
                    BuildScriptingBackend backend = platform.scriptingBackends[i];

                    if (keys[targetKey] == backend.name)
                    {
                        scriptingBackend = backend;
                        success = true;
                        break;
                    }
                }
            }

            if (scriptingBackend == null)
                return false;

            // TODO: Parse variants.

            // Parse distribution.
            if (keyCount > ++targetKey)
            {
                success = false;
                for (int i = 0; i < platform.distributionList.distributions.Length; i++)
                {
                    BuildDistribution dist = platform.distributionList.distributions[i];

                    if (keys[targetKey] == dist.distributionName)
                    {
                        distribution = dist;
                        success = true;
                        break;
                    }
                }
            }

            return success;
        }

        private string[] RefreshPlatforms(string keyChain, ConfigDictionary refreshedConfigSet, ConfigDictionary prevConfigSet)
        {
            List<string> childKeys = new List<string>();

            List<BuildPlatform> platforms = BuildSettings.platformList.platforms;
            for (int i = 0; i < platforms.Count; i++)
            {
                // Skip if platform is disabled or if it doesn't have any
                // enabled architectures or scripting backends
                if (!platforms[i].enabled || !platforms[i].atLeastOneArch || !platforms[i].atLeastOneBackend)
                    continue;

                string key = keyChain + "/" + platforms[i].platformName;
                Configuration relConfig = new Configuration();

                // Check for duplicate key.
                if (refreshedConfigSet.ContainsKey(key))
                    continue;

                // Copy previous settings if they exist.
                if (prevConfigSet != null && prevConfigSet.ContainsKey(key))
                {
                    relConfig.enabled = prevConfigSet[key].enabled;
                }

                // Refresh architectures.
                BuildArchitecture[] architectures = platforms[i].architectures;
                BuildScriptingBackend[] scriptingBackends = platforms[i].scriptingBackends;
                if (architectures.Length > 0)
                    relConfig.childKeys = RefreshArchitectures(key, refreshedConfigSet, platforms[i].variantKey, architectures,
                        scriptingBackends, platforms[i].distributionList.distributions, prevConfigSet);

                // Scan ahead for other versions of this platform with different variants.
                for (int j = i; j < platforms.Count; j++)
                {
                    BuildPlatform otherPlatform = platforms[j];
                    if (otherPlatform.platformName == platforms[i].platformName && otherPlatform.enabled && otherPlatform.atLeastOneArch
                        && otherPlatform.atLeastOneBackend)
                    {
                        List<string> currentKeys = new List<string>(relConfig.childKeys);
                        string[] additionalKeys = RefreshArchitectures(key, refreshedConfigSet, otherPlatform.variantKey, otherPlatform.architectures,
                            otherPlatform.scriptingBackends, otherPlatform.distributionList.distributions, prevConfigSet);

                        for (int k = 0; k < additionalKeys.Length; k++)
                        {
                            if (!currentKeys.Contains(additionalKeys[k]))
                                currentKeys.Add(additionalKeys[k]);
                        }

                        relConfig.childKeys = currentKeys.ToArray();
                    }
                }

                // Save configuration.
                refreshedConfigSet.Add(key, relConfig);

                // Add key to list to send back to parent.
                childKeys.Add(key);
            }

            return childKeys.ToArray();
        }

        private string[] RefreshArchitectures(string keyChain, ConfigDictionary refreshedConfigSet, string variantKey, BuildArchitecture[] architectures,
            BuildScriptingBackend[] scriptingBackends, BuildDistribution[] distributions, ConfigDictionary prevConfigSet)
        {
            List<string> childKeys = new List<string>();

            for (int i = 0; i < architectures.Length; i++)
            {
                // Skip if architecture is disabled.
                if (!architectures[i].enabled)
                    continue;

                string key = keyChain + "/" + architectures[i].name;
                if (variantKey.Length > 0)
                    key += " (" + variantKey + ")";

                Configuration relConfig = new Configuration();

                // Check for a duplicate key.
                if (refreshedConfigSet.ContainsKey(key))
                    continue;

                // Copy previous settings if they exist.
                if (prevConfigSet != null && prevConfigSet.ContainsKey(key))
                {
                    relConfig.enabled = prevConfigSet[key].enabled;
                }

                //Refresh scripting backends
                if (scriptingBackends.Length > 0)
                {
                    relConfig.childKeys = RefreshBackends(key, refreshedConfigSet, scriptingBackends, distributions, prevConfigSet);
                }
                else
                {
                    //If scripting backends is empty, don't miss the distributions
                    relConfig.childKeys = RefreshDistributions(key, refreshedConfigSet, distributions, prevConfigSet);
                }

                // Save configuration.
                refreshedConfigSet.Add(key, relConfig);

                // Add key to list to send back to parent.
                childKeys.Add(key);
            }

            return childKeys.ToArray();
        }

        private string[] RefreshBackends(string keyChain, ConfigDictionary refreshedConfigSet, BuildScriptingBackend[] scriptingBackends,
            BuildDistribution[] distributions, ConfigDictionary prevConfigSet)
        {
            List<string> childKeys = new List<string>();

            for (int i = 0; i < scriptingBackends.Length; i++)
            {
                // Skip if scripting backend is disabled.
                if (!scriptingBackends[i].enabled)
                    continue;

                string key = keyChain + "/" + scriptingBackends[i].name;

                Configuration relConfig = new Configuration();

                // Check for a duplicate key.
                if (refreshedConfigSet.ContainsKey(key))
                    continue;

                // Copy previous settings if they exist.
                if (prevConfigSet != null && prevConfigSet.ContainsKey(key))
                {
                    relConfig.enabled = prevConfigSet[key].enabled;
                }

                // Refresh distributions.
                if (distributions.Length > 0)
                {
                    relConfig.childKeys = RefreshDistributions(key, refreshedConfigSet, distributions, prevConfigSet);
                }

                // Save configuration.
                refreshedConfigSet.Add(key, relConfig);

                // Add key to list to send back to parent.
                childKeys.Add(key);
            }

            return childKeys.ToArray();
        }

        private string[] RefreshDistributions(string keyChain, ConfigDictionary refreshedConfigSet, BuildDistribution[] distributions, ConfigDictionary prevConfigSet)
        {
            List<string> childKeys = new List<string>();

            for (int i = 0; i < distributions.Length; i++)
            {
                if (!distributions[i].enabled)
                    continue;

                string key = keyChain + "/" + distributions[i].distributionName;
                Configuration relConfig = new Configuration();

                if (refreshedConfigSet.ContainsKey(key))
                    continue;

                if (prevConfigSet != null && prevConfigSet.ContainsKey(key))
                {
                    relConfig.enabled = prevConfigSet[key].enabled;
                }

                refreshedConfigSet.Add(key, relConfig);

                // Add key to list to send back to parent.
                childKeys.Add(key);
            }

            return childKeys.ToArray();
        }
    }
}
