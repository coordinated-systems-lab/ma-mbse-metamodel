using System.Collections.Generic;

namespace genesys_graphql
{
    public class MissionAwareSystemModelData
    {
        public MissionAwareSystemModel data { get; set; }
    }
    public class MissionAwareSystemModel
    {
        public Project project { get; set; }
        public IList<Component> components { get; set; }
        public IList<Interface> interfaces { get; set; }

    }
    public class Project
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string version { get; set; }
    }
    public class Component
    {
        public ComponentID identity { get; set; }
        public ComponentATTR attributes { get; set; }
        public ComponentREL relationships { get; set; }
    }
    public class ComponentID
    {
        public string id { get; set; }
        public string name { get; set; }
        public string number { get; set; }
    }
    public class ComponentATTR
    {
        public string description { get; set; }
        public string type { get; set; }
    }
    public class ComponentREL
    {
        public IList<BuiltFromTarget> builtFrom { get; set; }
        public IList<BuiltInTarget> builtIn { get; set; }
        public IList<JoinedToTarget> joinedTo { get; set; }
    }
    public class BuiltFromTarget
    {
        public ComponentID componentTarget { get; set; }
    }
    public class BuiltInTarget
    {
        public ComponentID componentTarget { get; set; }
    }
    public class JoinedToTarget
    {
        public InterfaceID interfaceTarget { get; set; }
    }
    public class Interface
    {
        public InterfaceID identity { get; set; }
        public InterfaceATTR attributes { get; set; }
        public InterfaceREL relationships { get; set; }
    }
    public class InterfaceID
    {
        public string id { get; set; }
        public string name { get; set; }
        public string number { get; set; }
    }
    public class InterfaceATTR
    {
        public string description { get; set; }
    }
    public class InterfaceREL
    {
        public IList<JoinsTarget> joins { get; set; }
    }
    public class JoinsTarget
    {
        public InterfaceID interfaceTarget { get; set; }
    }
}
