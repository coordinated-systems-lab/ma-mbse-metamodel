using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Vitech.Genesys.Client;
using Vitech.Genesys.Common;
using Vitech.Genesys.Client.Schema;

namespace genesys_graphql
{
    class GenesysQuery
    {
        static ClientModel client;
        static System.IO.StreamWriter schemaFile;
        static System.IO.StreamWriter classFile;
        static readonly Dictionary<string, string> _pluralTable = new Dictionary<string, string>
        {
            {"category", "categories"},
            {"loss", "losses"}
        };

        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "-s")
            {
                CreateSchema(args[1]);
            }
            else if (args.Length == 3 && args[0] == "-m")
            {
                CreateModel(args[1], args[2]);
            }
            else
            {
                Console.WriteLine("Usage: -s #create schema <schema: project name>, -m #create model <model: project name> <model: output file>");
            }
            client.Dispose();
            schemaFile.Close();
            classFile.Close();
            System.Environment.Exit(0);
        }
        static IProject Connect(string projectName)
        {
            // Setup Connection to GENESYS
            client = new ClientModel();
            RepositoryConfiguration repositoryConfiguration = client.GetKnownRepositories().LocalRepository;
            GenesysClientCredentials credentials = new GenesysClientCredentials("api-user", "api-pwd", AuthenticationType.GENESYS);
            repositoryConfiguration.Login(credentials);
            Console.WriteLine("Logged In!");

            // Select Project - from command line
            Repository repository = repositoryConfiguration.GetRepository();
            IProject project = repository.GetProject(projectName);
            Console.WriteLine("Project Id: " + project.Id);
            // Write GraphQL Schema header
            schemaFile = new System.IO.StreamWriter(@"..\..\..\schema\schema.graphql",false);
            schemaFile.WriteLine("schema {");
            schemaFile.WriteLine("  query: Query");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type Query {");
            schemaFile.WriteLine("  missionAwareSystemModel: MissionAwareSystemModel");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type MissionAwareSystemModel {");
            schemaFile.WriteLine("  # The project identity.");
            schemaFile.WriteLine("  project: Project");
            schemaFile.WriteLine("");

            // Write C# Class header
            classFile = new System.IO.StreamWriter(@"..\..\DataModel.cs");
            classFile.WriteLine("using System.Collections.Generic;");
            classFile.WriteLine("namespace genesys_graphql");
            classFile.WriteLine("{");
            classFile.WriteLine("  public class MissionAwareSystemModelData");
            classFile.WriteLine("  {");

            return project;
        }
        static void CreateSchema(string projectName)
        {
            var project = Connect(projectName);

            ISchema schema = project.GetSchema();
            IFacility facility = schema.GetFacility(new Guid("da424ed7-b58a-4496-af10-35adf696efd1")); // SE GUID
            Console.WriteLine("Facility: " + facility.Name + "  :" + facility.Id);
            IFacilityEntityDefinitionList facilityEntityDefinitionList = facility.EntityDefinitions;
            IEnumerator<IEntityDefinition> entityDefinitionList = facilityEntityDefinitionList.GetEnumerator();

            // Create a sorted list of Entities for SE Facility
            List<String> sortedEntityDefinitionList = new List<String>();
            for (var i = 0; i < facilityEntityDefinitionList.Count; i++)
            {
                entityDefinitionList.MoveNext();
                sortedEntityDefinitionList.Add(entityDefinitionList.Current.Name);
            }
            sortedEntityDefinitionList.Sort();
            // Output Schema (GraphQL & C#) for each Entity
            foreach (String entity in sortedEntityDefinitionList)
            {
                IEntityDefinition entityDefinition = schema.GetEntityDefinition(entity);
                List<string> commentLine = WrapLineComment(entityDefinition.Description.ToString());
                foreach (string line in commentLine)
                {
                    // Component description
                    schemaFile.WriteLine("  " + line);
                }
                // lowercase first character of Component and make plural variable name
                schemaFile.WriteLine("  " + GetPlural(Char.ToLowerInvariant(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1)) +
                    ": [" + entityDefinition.Name + "]");
                schemaFile.WriteLine("");
            }
            // Ouput Project Definition
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type Project {");
            schemaFile.WriteLine("  id: ID!");
            schemaFile.WriteLine("  name: String!");
            schemaFile.WriteLine("  description: String");
            schemaFile.WriteLine("  version: String");
            schemaFile.WriteLine("}");

            // Output Schema (GraphQL & C#) for each Entity
            foreach (String entity in sortedEntityDefinitionList)
            {
                IEntityDefinition entityDefinition = schema.GetEntityDefinition(entity);
                schemaFile.WriteLine("type " + entityDefinition.Name + " {");
                schemaFile.WriteLine("  identity: " + entityDefinition.Name + "ID!");
                schemaFile.WriteLine("  attributes: " + entityDefinition.Name + "ATTR");
                schemaFile.WriteLine("  relations: " + entityDefinition.Name + "REL");
                schemaFile.WriteLine("}");
                IEntityAttributeDefinitionList entityAttributeDefinitionList = entityDefinition.GetAttributeDefinitions();
                // Create a sorted list of Attributes for Entity
                List<String> sortedAttributeDefinitionList = new List<String>();
                foreach (IEntityAttributeDefinition entityAttributeDefinition in entityAttributeDefinitionList)
                {
                    sortedAttributeDefinitionList.Add(entityAttributeDefinition.Name);
                }
                sortedAttributeDefinitionList.Sort();
                foreach (String entityAttribute in sortedAttributeDefinitionList)
                {
                    IAttributeDefinition attributeDefinition = entityDefinition.GetAttributeDefinition(entityAttribute);
                    Console.WriteLine("   A:" + attributeDefinition.Name + ":" + attributeDefinition.DataType.ToString());
                }
                IEnumerable<IRelationDefinition> entityRelationshipDefinitionList = entityDefinition.GetRelationDefinitions();
                // Create a sorted list of Entities for SE Facility
                List<String> sortedEntityRelationList = new List<String>();
                foreach (IRelationDefinition relationDefinition in entityRelationshipDefinitionList)
                {
                    Console.WriteLine("   R:" + relationDefinition.Name);
                }
            }
        }
        static void CreateModel( string projectName, string outFileName)
        {
            var project = Connect(projectName);
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
        }
        // Find Plural
        static string GetPlural(string word)
        {
            try
            {
                return (_pluralTable[word]);
            }
            catch
            {
                return (word + "s");
            }
        }
        // Wrap multiline comments
        static List<string> WrapLineComment(string text)
        {
            int start = 0, margin = 80, end;
            var lines = new List<string>();
            text = Regex.Replace(text, @"\s", " ").Trim();

            while ((end = start + margin) < text.Length)
            {
                while (text[end] != ' ' && end > start)
                    end -= 1;

                if (end == start)
                    end = start + margin;

                lines.Add("# " + text.Substring(start, end - start));
                start = end + 1;
            }

            if (start < text.Length)
                lines.Add("# " + text.Substring(start));

            return lines;
        }
    }
}


// Console.WriteLine(typeof(Project).AssemblyQualifiedName);
// const string objectToInst = "genesys_graphql.Project,genesys-graphql";

// var objectType = Type.GetType(objectToInst);
// Console.WriteLine(objectType);

// dynamic instObject = Activator.CreateInstance(objectType);
// instObject.version = "123";
// Console.WriteLine(instObject.version);
// string json1 = JsonConvert.SerializeObject(instObject, Formatting.Indented);
// Console.WriteLine(json1);