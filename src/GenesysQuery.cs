using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitech.Genesys;
using Vitech.Genesys.Client;
using Vitech.Genesys.Common;
using Vitech.Genesys.License;
using Vitech.Genesys.License.Provider;

namespace genesys_graphql
{
    class GenesysQuery
    {
        static void Main(string[] args)
        {
            // Setup Connection to GENESYS
            ClientModel client = new ClientModel();
            RepositoryConfiguration repositoryConfiguration = client.GetKnownRepositories().LocalRepository;
            GenesysClientCredentials credentials = new GenesysClientCredentials("api-user", "api-pwd", AuthenticationType.GENESYS);
            repositoryConfiguration.Login(credentials);
            Console.WriteLine("Logged In!");

            // Select Project - from command line
            Repository repository = repositoryConfiguration.GetRepository();
            IProject project = repository.GetProject(args[0]);
            Console.WriteLine("Project Id: " + project.Id);
            MissionAwareSystemModelData modelData = new MissionAwareSystemModelData
            {
                data = new MissionAwareSystemModel
                {
                    project = new Project
                    {
                        id = project.Id.ToString(),
                        name = project.Name,
                        description = project.Description?.PlainText.Replace(Environment.NewLine, "\n") ?? null,
                        version = project.Version?.ToString() ?? null
                    }
                }
            };
            modelData.data.components = new List<Component>();
            modelData.data.interfaces = new List<Interface>();
            ISortBlock numericSortBlock = project.GetSortBlock(SortBlockConstants.Numeric);
            int entityIndex;
            IFolder folder;
            IEnumerable<IEntity> entityList;
 
            // Output Components
            folder = project.GetFolder("Component");
            entityList = folder.GetAllEntities();
            entityIndex = 0;
            foreach (IEntity entity in numericSortBlock.SortEntities(entityList))
            {
                modelData.data.components.Add(new Component
                {
                    identity = new ComponentID
                    {
                        id = entity.Id.ToString(),
                        name = entity?.Name ?? null,
                        number = entity.GetAttribute("number")?.ToString() ?? null
                    },
                    attributes = new ComponentATTR
                    {
                        description = entity.GetAttribute("description")?.ToString().Replace(Environment.NewLine, "\n") ?? null,
                        type = entity.GetAttribute("type").ToString().Replace(" ", "_")
                    }
                });
                modelData.data.components[entityIndex].relationships = new ComponentREL
                {
                    builtFrom = new List<ComponentID>(),
                    builtIn = new List<ComponentID>(),
                    joinedTo = new List<InterfaceID>()
                };
                foreach (IEntity builtFromEntity in entity.GetRelationshipTargets("built from"))
                {
                    modelData.data.components[entityIndex].relationships.builtFrom.Add(new ComponentID
                    {
                        id = builtFromEntity.Id.ToString(),
                        name = builtFromEntity?.Name ?? null,
                        number = builtFromEntity.GetAttributeValue("number")?.ToString() ?? null
                    });
                }
                foreach (IEntity builtInEntity in entity.GetRelationshipTargets("built in"))
                {
                    modelData.data.components[entityIndex].relationships.builtIn.Add(new ComponentID
                    {
                        id = builtInEntity.Id.ToString(),
                        name = builtInEntity?.Name ?? null,
                        number = builtInEntity.GetAttributeValue("number")?.ToString() ?? null
                    });
                }
                foreach (IEntity joinedToEntity in entity.GetRelationshipTargets("joined to"))
                {
                    modelData.data.components[entityIndex].relationships.joinedTo.Add(new InterfaceID
                    {
                        id = joinedToEntity.Id.ToString(),
                        name = joinedToEntity?.Name ?? null,
                        number = joinedToEntity.GetAttributeValue("number")?.ToString() ?? null
                    });
                }
                entityIndex++;
            }
            // Output Interfaces
            folder = project.GetFolder("Interface");
            entityList = folder.GetAllEntities();
            entityIndex = 0;
            foreach (IEntity entity in numericSortBlock.SortEntities(entityList))
            {
                modelData.data.interfaces.Add(new Interface
                {
                    identity = new InterfaceID
                    {
                        id = entity.Id.ToString(),
                        name = entity?.Name ?? null,
                        number = entity.GetAttribute("number")?.ToString() ?? null
                    },
                    attributes = new InterfaceATTR
                    {
                        description = entity.GetAttribute("description")?.ToString().Replace(Environment.NewLine, "\n") ?? null,
                    }
                });
                modelData.data.interfaces[entityIndex].relationships = new InterfaceREL
                {
                    joins = new List<ComponentID>()
                };
                foreach (IEntity joinsEntity in entity.GetRelationshipTargets("joins"))
                {
                    modelData.data.interfaces[entityIndex].relationships.joins.Add(new ComponentID
                    {
                        id = joinsEntity.Id.ToString(),
                        name = joinsEntity?.Name ?? null,
                        number = joinsEntity.GetAttributeValue("number")?.ToString() ?? null
                    });
                }
                entityIndex++;
            }
            // Ouput Links

            string json = JsonConvert.SerializeObject(modelData, Formatting.Indented);
            Console.WriteLine(json);
        }
    }
    public class MissionAwareSystemModelData
    {
        public MissionAwareSystemModel data { get; set; }
    }
    public class MissionAwareSystemModel
    {
        public Project project { get; set; }
        public IList<Component> components { get; set;}
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
        public IList<ComponentID> builtFrom { get; set; }
        public IList<ComponentID> builtIn { get; set; }
        public IList<InterfaceID> joinedTo { get; set; }
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
        public IList<ComponentID> joins { get; set; }
    }
}
