using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vitech.Genesys.Client;
using Vitech.Genesys.Common;

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
                    builtFrom = new List<BuiltFromTarget>(),
                    builtIn = new List<BuiltInTarget>(),
                    joinedTo = new List<JoinedToTarget>()
                };
                foreach (IEntity builtFromEntity in entity.GetRelationshipTargets("built from"))
                {
                    modelData.data.components[entityIndex].relationships.builtFrom.Add(new BuiltFromTarget
                    {
                        componentTarget = new ComponentID
                        {
                            id = builtFromEntity.Id.ToString(),
                            name = builtFromEntity?.Name ?? null,
                            number = builtFromEntity.GetAttributeValue("number")?.ToString() ?? null
                        }

                    });
                }
                foreach (IEntity builtInEntity in entity.GetRelationshipTargets("built in"))
                {
                    modelData.data.components[entityIndex].relationships.builtIn.Add(new BuiltInTarget
                    {
                        componentTarget = new ComponentID
                        {
                            id = builtInEntity.Id.ToString(),
                            name = builtInEntity?.Name ?? null,
                            number = builtInEntity.GetAttributeValue("number")?.ToString() ?? null
                        }
                    });
                }
                foreach (IEntity joinedToEntity in entity.GetRelationshipTargets("joined to"))
                {
                    modelData.data.components[entityIndex].relationships.joinedTo.Add(new JoinedToTarget
                    {
                        interfaceTarget = new InterfaceID
                        {
                            id = joinedToEntity.Id.ToString(),
                            name = joinedToEntity?.Name ?? null,
                            number = joinedToEntity.GetAttributeValue("number")?.ToString() ?? null
                        }
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
                    joins = new List<JoinsTarget>()
                };
                foreach (IEntity joinsEntity in entity.GetRelationshipTargets("joins"))
                {
                    modelData.data.interfaces[entityIndex].relationships.joins.Add(new JoinsTarget
                    {
                        interfaceTarget = new InterfaceID
                        {
                            id = joinsEntity.Id.ToString(),
                            name = joinsEntity?.Name ?? null,
                            number = joinsEntity.GetAttributeValue("number")?.ToString() ?? null
                        }
                    });
                }
                entityIndex++;
            }
            // Ouput Links

            // Output as JSON document
            string json = JsonConvert.SerializeObject(modelData, Formatting.Indented);
            Console.WriteLine(json);
            client.Dispose();
            System.Environment.Exit(0);
        }
    }
}