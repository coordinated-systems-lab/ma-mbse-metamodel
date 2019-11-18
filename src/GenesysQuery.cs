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


            return project;
        }
        static void CreateSchema(string projectName)
        {
            var project = Connect(projectName);
            ISchema schema = project.GetSchema();
            IFacility facility = schema.GetFacility(new Guid("da424ed7-b58a-4496-af10-35adf696efd1")); // SE GUID
            Console.WriteLine("Facility: " + facility.Name + "  :" + facility.Id);

            // Write GraphQL Schema header
            schemaFile = new System.IO.StreamWriter(@"..\..\..\schema\schema.graphql", false);
            schemaFile.WriteLine("schema {");
            schemaFile.WriteLine("  query: Query");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type Query {");
            schemaFile.WriteLine("  # System Model for: '" + facility.Name + "' Facility");
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
                schemaFile.WriteLine("#########################################");
                schemaFile.WriteLine("# " + entityDefinition.Name + " definition");
                schemaFile.WriteLine("#########################################");
                schemaFile.WriteLine("type " + entityDefinition.Name + " {");
                schemaFile.WriteLine("  identity: " + entityDefinition.Name + "ID!");
                schemaFile.WriteLine("  attributes: " + entityDefinition.Name + "ATTR");
                if (entityDefinition.GetParameterDefinitions().Count != 0)
                {
                    // Entity has Parameters defined
                    schemaFile.WriteLine("  parameters: " + entityDefinition.Name + "PARAM");
                }
                schemaFile.WriteLine("  relations: " + entityDefinition.Name + "REL");
                schemaFile.WriteLine("}");

                // Output entity identity
                schemaFile.WriteLine("type " + entityDefinition.Name + "ID {");
                schemaFile.WriteLine("  id: ID!");
                schemaFile.WriteLine("  name: String!");
                schemaFile.WriteLine("  number: String!");
                schemaFile.WriteLine("}");

                IEntityAttributeDefinitionList entityAttributeDefinitionList = entityDefinition.GetAttributeDefinitions();
                // Create a sorted list of Attributes for Entity
                List<String> sortedAttributeDefinitionList = new List<String>();
                foreach (IEntityAttributeDefinition entityAttributeDefinition in entityAttributeDefinitionList)
                {
                    sortedAttributeDefinitionList.Add(entityAttributeDefinition.Name);
                }
                sortedAttributeDefinitionList.Sort();
                
                schemaFile.WriteLine("type " + entityDefinition.Name + "ATTR {");

                List<String> enumAttributeList = new List<String>();
                foreach (String entityAttribute in sortedAttributeDefinitionList)
                {
                    if (entityAttribute == "name" || entityAttribute == "number")
                    {
                        continue; // name and number are part of "identity"
                    }
                    IEntityAttributeDefinition entityAttributeDefinition = entityDefinition.GetAttributeDefinition(entityAttribute);
                    DataTypeDefinition entityAttributeType = entityAttributeDefinition.DataType;
                    if (entityAttributeType.ToString() == "Vitech.Genesys.Common.ScriptSpecTypeDefinition")
                    {
                        continue; // Skip script attributes
                    }
                    List<string> commentLine = WrapLineComment(entityAttributeDefinition.Description.ToString());
                    foreach (string line in commentLine)
                    {
                        // Entity description
                        schemaFile.WriteLine("  " + line);
                    }

                    string entityAttributeDefinitionName = entityAttributeDefinition.Name.Replace("-","_");

                    switch (entityAttributeType.ToString())
                    {
                        case "Vitech.Genesys.Common.FormattedTextTypeDefinition":
                        case "Vitech.Genesys.Common.NumberSpecTypeDefinition":
                        case "Vitech.Genesys.Common.ReferenceSpecTypeDefinition":
                        case "Vitech.Genesys.Common.StringTypeDefinition":
                        case "Vitech.Genesys.Common.DateTimeTypeDefinition":
                        case "Vitech.Genesys.Common.DateTypeDefinition":
                        case "Vitech.Genesys.Common.HierarchicalNumberTypeDefinition":
                            schemaFile.WriteLine("  " + entityAttributeDefinitionName + ": String");
                            break;
                        case "Vitech.Genesys.Common.BooleanTypeDefinition":
                            schemaFile.WriteLine("  " + entityAttributeDefinitionName + ": Boolean");
                            break;
                        case "Vitech.Genesys.Common.FloatTypeDefinition":
                            schemaFile.WriteLine("  " + entityAttributeDefinitionName + ": Float");
                            break;
                        case "Vitech.Genesys.Common.EnumerationTypeDefinition":
                            EnumerationTypeDefinition enumDefinition = entityAttributeDefinition.DataType as EnumerationTypeDefinition;
                            var capEnumAttributeName = Char.ToUpperInvariant(entityAttributeDefinitionName[0]) +
                                entityAttributeDefinitionName.Substring(1);
                            // Enum type is: <EntityName><AttributeName> 
                            schemaFile.WriteLine("  " + entityAttributeDefinitionName + ": " + entityDefinition.Name + capEnumAttributeName);
                            enumAttributeList.Add(entityAttributeDefinition.Name);
                            break;
                        case "Vitech.Genesys.Common.IntegerTypeDefinition":
                            schemaFile.WriteLine("  " + entityAttributeDefinitionName + ": Integer");
                            break;
                        case "Vitech.Genesys.Common.CollectionTypeDefinition":
                            CollectionTypeDefinition collectionDefinition = entityAttributeDefinition.DataType as CollectionTypeDefinition;
                            switch (collectionDefinition.ValueType.ToString())
                            {
                                case "Vitech.Genesys.Common.StringTypeDefinition":
                                    schemaFile.WriteLine("  " + entityAttributeDefinitionName + ": [String]");
                                    break;
                                default:
                                    Console.WriteLine("Missing Collection Type!");
                                    break;
                            }
                            break;
                        default:
                            Console.WriteLine("Missing Type!");
                            break;
                    }
                    schemaFile.WriteLine(""); // leave space between attribute definitions
                                        
                }
                schemaFile.WriteLine("}");

                // Ouput Enum types
                foreach (String enumAttribute in enumAttributeList)
                {
                    var capEnumAttributeName = Char.ToUpperInvariant(enumAttribute[0]) +
                                enumAttribute.Substring(1);
                    schemaFile.WriteLine("enum " + entityDefinition.Name + capEnumAttributeName.Replace("-", "_") + " {");
                    IEntityAttributeDefinition entityAttributeDefinition = entityDefinition.GetAttributeDefinition(enumAttribute);
                    EnumerationTypeDefinition enumDefinition = entityAttributeDefinition.DataType as EnumerationTypeDefinition;

                    EnumPossibleValue[] enumPossibleValues = enumDefinition.PossibleValues;
                    for (var i = 0; i< enumPossibleValues.Length; i++)
                    {

                        String enumValue = enumPossibleValues[i].ToString().Replace("/", "_").Replace(" ", "_").Replace("-", "").Replace("&", "").Replace(":", "");
                        if (Char.IsDigit(enumValue.ToString()[0]))
                        {
                            // Enum canot begin with a digit - prepend "E_"
                            enumValue = "E_" + enumValue;
                        }
                        schemaFile.WriteLine("  " + enumValue);
                    }
                    schemaFile.WriteLine("}");
                }

                IEnumerable<IRelationDefinition> entityRelationDefinitionList = entityDefinition.GetRelationDefinitions();
                // Create a sorted list of Relations for Entity
                List<String> sortedEntityRelationList = new List<String>();
                foreach (IRelationDefinition entityRelationDefinition in entityRelationDefinitionList)
                {
                    sortedEntityRelationList.Add(entityRelationDefinition.Name);
                }
                sortedEntityRelationList.Sort();

                schemaFile.WriteLine("type " + entityDefinition.Name + "REL {");
                foreach (String relationDefinitionName in sortedEntityRelationList)
                {
                    String camelRelationName = GetCamelCaseRelation(relationDefinitionName);

                    IRelationDefinition relationDefinition = schema.GetRelationDefinition(relationDefinitionName);

                    List<string> commentLine = WrapLineComment(relationDefinition.Description.ToString());
                    foreach (string line in commentLine)
                    {
                        // Relation description
                        schemaFile.WriteLine("  " + line);
                    }
                    schemaFile.WriteLine("  " + camelRelationName + ": [" +
                        Char.ToUpper(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) + "_" +
                        Char.ToUpperInvariant(camelRelationName[0]) +
                        camelRelationName.Substring(1) + "Target]");

                    schemaFile.WriteLine(""); // space between relationships

                }
                schemaFile.WriteLine("}");

                // Output Relationship Target
                foreach (String relationDefinitionName in sortedEntityRelationList)
                {
                    String camelRelationName = GetCamelCaseRelation(relationDefinitionName);

                    schemaFile.WriteLine("type " +
                        Char.ToUpper(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) + "_" +
                        Char.ToUpperInvariant(camelRelationName[0]) +
                        camelRelationName.Substring(1) + "Target {");
                    IRelationDefinition relationDefinition = schema.GetRelationDefinition(relationDefinitionName);
                    IEnumerable<IEntityDefinition> targetEntityDefinitionList = 
                        entityDefinition.GetTargetEntityDefinitions(relationDefinition);
                    foreach( IEntityDefinition targetEntityDefinition in targetEntityDefinitionList )
                    {
                        if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                        {
                            // Only include Target if included in selected "Facility"
                            schemaFile.WriteLine("  " + targetEntityDefinition.Name + "Target: " +
                                Char.ToUpperInvariant(targetEntityDefinition.Name[0]) +
                                targetEntityDefinition.Name.Substring(1) + "ID");
                        }
                    }
                    schemaFile.WriteLine("}");
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
        // CamelCase Relation Name
        static string GetCamelCaseRelation(string relation)
        {
            string camelRelationName = "";
            string[] relationNameParts = relation.Split(' ');
            for (var i = 0; i < relationNameParts.Length; i++)
            {
                if (i == 0)
                {
                    camelRelationName += relationNameParts[i];
                }
                else
                {
                    camelRelationName += Char.ToUpperInvariant(relationNameParts[i][0]) +
                        relationNameParts[i].Substring(1);
                }
            }
            return camelRelationName;
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