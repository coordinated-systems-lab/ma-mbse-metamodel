using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Vitech.Genesys.Client;
using Vitech.Genesys.Common;
using Vitech.Genesys.Client.Schema;

namespace genesys_graphql
{
    class GenesysQuery
    {
        static ClientModel client;
        static System.IO.StreamWriter schemaFile;
        static System.IO.StreamWriter dataFile;

        static int indent = 0;
        static List<String> sortedEntityDefinitionList = new List<String>();
        static ISchema schema;
        static string facilityName;
        static string dataFileLine;

        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "-s")
            {
                CreateSchema(args[1]);
                schemaFile.Close();
            }
            else if (args.Length == 3 && args[0] == "-m")
            {
                CreateModel(args[1], args[2]);
                dataFile.Close();
            }
            else
            {
                Console.WriteLine("Usage: -s #create schema <schema: project name>, -m #create model <model: project name> <model: output file>");
            }
            client.Dispose();
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

            schema = project.GetSchema();
            // IFacilityList facilityList = schema.GetFacilities();
            // IFacility facility = schema.GetFacility(new Guid("da424ed7-b58a-4496-af10-35adf696efd1")); // SE GUID
            IFacility facility = schema.GetFacility(new Guid("8b0c834f-8039-4129-837a-98b8240a07ea")); // MA GUID

            Console.WriteLine("Facility: " + facility.Name + "  :" + facility.Id);
            facilityName = facility.Name;

            IFacilityEntityDefinitionList facilityEntityDefinitionList = facility.EntityDefinitions;
            IEnumerator<IEntityDefinition> entityDefinitionList = facilityEntityDefinitionList.GetEnumerator();

            // Create a sorted list of Entities for SE Facility
            sortedEntityDefinitionList = new List<String>();
            for (var i = 0; i < facilityEntityDefinitionList.Count; i++)
            {
                entityDefinitionList.MoveNext();
                sortedEntityDefinitionList.Add(entityDefinitionList.Current.Name);
                IEnumerable<IEntityDefinition> childEntityDefinitionList = entityDefinitionList.Current.GetAllChildrenConcrete();
                foreach (IEntityDefinition childEntityDefinition in childEntityDefinitionList)
                {
                    sortedEntityDefinitionList.Add(childEntityDefinition.Name);
                }
            }
            sortedEntityDefinitionList.Sort();

            return project;
        }
        static void CreateStructure(IProject project)
        {
            dataFile.WriteLine("".PadLeft(indent) + @"""callStructure"": [");
            IFolder functionFolder = project.GetFolder("Function");
            IEnumerable<IEntity> functionList = functionFolder.GetAllEntities();
            ISortBlock numericSortBlock = project.GetSortBlock(SortBlockConstants.Numeric);

            int currentFunctionIndex = 0;
            int totalFunctionCount = functionList.Count();

            foreach (IEntity function in numericSortBlock.SortEntities(functionList))
            {
                currentFunctionIndex += 1;
                indent += 2;
                dataFile.WriteLine("".PadLeft(indent) + "{");
                indent += 2;
                dataFile.WriteLine("".PadLeft(indent) + @"""function"": {");
                indent += 2;
                dataFile.WriteLine("".PadLeft(indent) + @"""id"": """ + function.Id.ToString() + @""",");
                dataFile.WriteLine("".PadLeft(indent) + @"""name"": """ + (function?.Name ?? null) + @""",");
                dataFile.WriteLine("".PadLeft(indent) + @"""number"": """ +
                    (function.GetAttribute("number")?.ToString() ?? null) + @"""");
                indent -= 2;
                dataFile.WriteLine("".PadLeft(indent) + "},");

                dataFile.WriteLine("".PadLeft(indent) + @"""structure"": {");
                indent += 2;

                IEnumerable<IStructureItem> structureItemList = function.GetStructure().GetItems<IStructureItem>();
                string structure = RetrieveStructureItem(structureItemList.First());
                dataFile.Write(structure);
                indent -= 2;
                dataFile.WriteLine("".PadLeft(indent) + "}");

                indent -= 2;
                if (currentFunctionIndex < totalFunctionCount)
                {
                    dataFile.WriteLine("".PadLeft(indent) + "},");
                }
                else
                { // last item - no ,
                    dataFile.WriteLine("".PadLeft(indent) + "}");
                }
                indent -= 2;
            }
            dataFile.WriteLine("".PadLeft(indent) + "]");
        }
        static string RetrieveStructureItem(IStructureItem inputStructureItem)
        {
            string structure = "";
            structure += "".PadLeft(indent) + @"""id"": """ + inputStructureItem.Id.ToString() + @"""," + Environment.NewLine;
            string structureType = inputStructureItem.GetType().Name;
            switch (structureType)
            {
                case "StructureBranch":
                    structure += "".PadLeft(indent) + @"""type"": ""Branch""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": ";
                    structure += (inputStructureItem as IStructureBranch).Annotation != null ?
                        @"""" + (inputStructureItem as IStructureBranch).Annotation.ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": null," + Environment.NewLine;
                    break;
                case "KillStatusBranch": // Parallel
                    structure += "".PadLeft(indent) + @"""type"": ""Branch""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": ";
                    structure += (inputStructureItem as IKillStatusBranch).Annotation != null ?
                        @"""" + (inputStructureItem as IKillStatusBranch).Annotation.ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": null," + Environment.NewLine;
                    break;
                case "SelectionBranch":
                    structure += "".PadLeft(indent) + @"""type"": ""Branch""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": ";
                    structure += (inputStructureItem as ISelectionBranch).Annotation != null ?
                        @"""" + (inputStructureItem as ISelectionBranch).Annotation.ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": null," + Environment.NewLine;
                    break;
                case "ParallelConstruct":
                    structure += "".PadLeft(indent) + @"""type"": ""Parallel""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": null," + Environment.NewLine;
                    break;
                case "SelectConstruct":
                    structure += "".PadLeft(indent) + @"""type"": ""Select""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": null," + Environment.NewLine;
                    break;
                case "LoopConstruct":
                    structure += "".PadLeft(indent) + @"""type"": ""Loop""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": null," + Environment.NewLine;
                    break;
                case "ReplicateConstruct": // referenced DomainSet
                    IEntity domainSet = inputStructureItem.Project.GetEntity((inputStructureItem as IReplicateConstruct).DomainSetId.Value);

                    structure += "".PadLeft(indent) + @"""type"": ""Replicate""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": ";
                    structure += (inputStructureItem as IReplicateConstruct).CoordinationBranch.Annotation != null ?
                        @"""" + (inputStructureItem as IReplicateConstruct).CoordinationBranch.Annotation.ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": """ +
                        domainSet.Id.ToString() + @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": """ + domainSet.Name + 
                        @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": ";
                    structure += domainSet.GetAttribute("number").ToString() != "" ?
                        @"""" + domainSet.GetAttribute("number").ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    break;
                case "FunctionConstruct":
                    IEntity function = inputStructureItem.Project.GetEntity((inputStructureItem as IFunctionConstruct).FunctionId);

                    structure += "".PadLeft(indent) + @"""type"": ""Function""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": """ +
                        function.Id.ToString() + @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": """ + function.Name +
                        @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": ";
                    structure += function.GetAttribute("number").ToString() != "" ?
                        @"""" + function.GetAttribute("number").ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    break;
                case "ExitBranch": // referenced Exit
                    IEntity exitB = inputStructureItem.Project.GetEntity((inputStructureItem as IExitBranch).ExitId);

                    structure += "".PadLeft(indent) + @"""type"": ""ExitCondition""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": """ +
                        exitB.Id.ToString() + @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": """ + exitB.Name +
                        @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": ";
                    structure += exitB.GetAttribute("number").ToString() != "" ?
                        @"""" + exitB.GetAttribute("number").ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    break; 
                case "ExitConstruct": // referenced Exit
                    IEntity exitC = inputStructureItem.Project.GetEntity((inputStructureItem as IExitConstruct).ExitId);

                    structure += "".PadLeft(indent) + @"""type"": ""Exit""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": """ +
                        exitC.Id.ToString() + @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": """ + exitC.Name +
                        @"""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": ";
                    structure += exitC.GetAttribute("number").ToString() != "" ?
                        @"""" + exitC.GetAttribute("number").ToString() + @"""," : "null,";
                    structure += Environment.NewLine;
                    break;
                case "LoopExitConstruct":
                    structure += "".PadLeft(indent) + @"""type"": ""LoopExit""," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""annotation"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceID"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceName"": null," + Environment.NewLine;
                    structure += "".PadLeft(indent) + @"""referenceNum"": null," + Environment.NewLine;
                    break;
            }

            IEnumerable<IStructureItem> structureItemList = inputStructureItem.GetLocalItems<IStructureItem>();
            int structureItemCount = structureItemList.Count();
            int currentStructureItem = 0;

            if (structureItemCount > 0)
            {
                structure += "".PadLeft(indent) + @"""structure"": [" + Environment.NewLine;
                indent += 2;

                foreach (IStructureItem structureItem in structureItemList)
                {
                    currentStructureItem += 1;
                    structure += "".PadLeft(indent) + @"{" + Environment.NewLine;
                    indent += 2;
                    structure += RetrieveStructureItem(structureItem);
                    indent -= 2;
                    if (currentStructureItem < structureItemCount)
                    {
                        // additional structure items to process - add ,
                        structure += "".PadLeft(indent) + @"}," + Environment.NewLine;
                    }
                    else
                    {
                        structure += "".PadLeft(indent) + @"}" + Environment.NewLine;
                    }
                }

                indent -= 2;
                structure += "".PadLeft(indent) + @"]" + Environment.NewLine;
            }
            else
            {
                structure += "".PadLeft(indent) + @"""structure"": []" + Environment.NewLine;
            }
            return (structure);
        }
        static void CreateSchema(string projectName)
        {
            Connect(projectName);

            // Write GraphQL Schema header
            schemaFile = new System.IO.StreamWriter(@"..\..\..\schema\cps-metamodel.graphql", false);
            schemaFile.WriteLine("schema {");
            schemaFile.WriteLine("  query: Query");
            schemaFile.WriteLine("  mutation: Mutation");

            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type Query {");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  List of Projects");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  cpsProjects: [Project]");

            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  System Model for: '" + facilityName + "' Facility");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  cpsSystemModel(projectId: ID!): CPSsystemModel");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type CPSsystemModel {");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  The project identity.");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  project: Project");
            schemaFile.WriteLine("");

            // Output Schema for each Entity
            foreach (String entity in sortedEntityDefinitionList)
            {
                IEntityDefinition entityDefinition = schema.GetEntityDefinition(entity);
                List<string> commentLine = WrapLineComment(entityDefinition.Description.ToString());
                foreach (string line in commentLine)
                {
                    // Component description
                    schemaFile.WriteLine("  " + line);
                }
                // lowercase first character of Component
                schemaFile.WriteLine("  " + Char.ToLower(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) +
                    ": [" + entityDefinition.Name + "]");
                schemaFile.WriteLine("");
            }
            // Output call Structure
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  recursive call structure (select, parallel, loop, etc.) for each function");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  callStructure: [CallStructure]");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("");


            //Output Mutations
            schemaFile.WriteLine("type Mutation {");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  Mutate Project");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  cpsProject(project: Project_Input): Project");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  Mutate CPS System Model");
            schemaFile.WriteLine(@"  """"""");
            schemaFile.WriteLine("  cpsSystemModel(projectId: ID!, cpsSystemModel: CPSsystemModel_Input): CPSSystemModel");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("");

            //Sytem Model input for mutation
            schemaFile.WriteLine("input CPSsystemModel_Input {");
            foreach (String entity in sortedEntityDefinitionList)
            {
                IEntityDefinition entityDefinition = schema.GetEntityDefinition(entity);
                schemaFile.WriteLine("  " + Char.ToLower(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) +
                    ": [" + entityDefinition.Name + "_Input]");
            }
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("");


            schemaFile.WriteLine("#########################################");
            schemaFile.WriteLine("# Common Definitions");
            schemaFile.WriteLine("#########################################");
            // Output Project Definition
            schemaFile.WriteLine("type Project {");
            schemaFile.WriteLine("  id: ID!");
            schemaFile.WriteLine("  name: String!");
            schemaFile.WriteLine("  description: String");
            schemaFile.WriteLine("  version: String");
            schemaFile.WriteLine("}");

            schemaFile.WriteLine("# for mutations");
            schemaFile.WriteLine("input Project_Input {");
            schemaFile.WriteLine("  operation: MutationOperation!");
            schemaFile.WriteLine("  id: ID # autogenerated on Create, required for Update / Delete");
            schemaFile.WriteLine("  name: String!");
            schemaFile.WriteLine("  description: String");
            schemaFile.WriteLine("  version: String");
            schemaFile.WriteLine("}");

            // Ouput Parameter Definition
            schemaFile.WriteLine("type Parameter {");
            schemaFile.WriteLine("  name: String!");
            schemaFile.WriteLine("  description: String");
            schemaFile.WriteLine("  objective: String");
            schemaFile.WriteLine("  threshold: String");
            schemaFile.WriteLine("  design: String");
            schemaFile.WriteLine("  observed: String");
            schemaFile.WriteLine("  units: String");
            schemaFile.WriteLine("}");

            // Ouput Parameter Definition as mutation input
            schemaFile.WriteLine("input Parameter_Input {");
            schemaFile.WriteLine("  operation: MutationOperation!");
            schemaFile.WriteLine("  name: String!");
            schemaFile.WriteLine("  description: String");
            schemaFile.WriteLine("  objective: String");
            schemaFile.WriteLine("  threshold: String");
            schemaFile.WriteLine("  design: String");
            schemaFile.WriteLine("  observed: String");
            schemaFile.WriteLine("  units: String");
            schemaFile.WriteLine("}");

            schemaFile.WriteLine(@"""""""");
            schemaFile.WriteLine("Mutations for List items of an Entity (Parameters, Relations) include an 'instance' operation.");
            schemaFile.WriteLine("NOTE: when 'creating' an Entity, all associated List item instances must be set to 'Create'");
            schemaFile.WriteLine("      when 'updating' an Entity, only include associated List items to be 'Created', 'Updated', or 'Deleted'");
            schemaFile.WriteLine("      when 'deleting' an Entity, all associated List items are automatically deleted");
            schemaFile.WriteLine(@"""""""");

            schemaFile.WriteLine("enum MutationOperation");
            schemaFile.WriteLine("{");
            schemaFile.WriteLine("  Create");
            schemaFile.WriteLine("  Update");
            schemaFile.WriteLine("  Delete");
            schemaFile.WriteLine("}");

            // Output Schema for each Entity
            foreach (String entity in sortedEntityDefinitionList)
            {
                IEntityDefinition entityDefinition = schema.GetEntityDefinition(entity);
                schemaFile.WriteLine("#########################################");
                schemaFile.WriteLine("# " + entityDefinition.Name + " definition");
                schemaFile.WriteLine("#########################################");
                schemaFile.WriteLine("type " + entityDefinition.Name + " {");
                schemaFile.WriteLine("  identity: " + entityDefinition.Name + "ID!");
                schemaFile.WriteLine("  attributes: " + entityDefinition.Name + "ATTR");
                schemaFile.WriteLine("  parameters: [Parameter]");
                schemaFile.WriteLine("  relations: " + entityDefinition.Name + "REL");
                schemaFile.WriteLine("}");

                schemaFile.WriteLine("# for mutations");
                schemaFile.WriteLine("input " + entityDefinition.Name + " {");
                schemaFile.WriteLine("  operation: MutationOperation!");
                schemaFile.WriteLine("  identity: " + entityDefinition.Name + "ID_Input!");
                schemaFile.WriteLine("  attributes: " + entityDefinition.Name + "ATTR_Input");
                schemaFile.WriteLine("  parameters: [Parameter_Input]");
                schemaFile.WriteLine("  relations: " + entityDefinition.Name + "REL_Input");
                schemaFile.WriteLine("}");

                // Output entity identity
                schemaFile.WriteLine("type " + entityDefinition.Name + "ID {");
                schemaFile.WriteLine("  id: ID!");
                schemaFile.WriteLine("  name: String!");
                schemaFile.WriteLine("  number: String!");
                schemaFile.WriteLine("}");

                // Output entity identity as input for mutations
                schemaFile.WriteLine("# for mutations");
                schemaFile.WriteLine("input " + entityDefinition.Name + "ID_Input {");
                schemaFile.WriteLine("  id: ID # autogenerated on Create, required for Update / Delete");
                schemaFile.WriteLine("  name: String!");
                schemaFile.WriteLine("  number: String!");
                schemaFile.WriteLine("}");

                IEnumerable<IAttributeDefinition> attributeDefinitionList = entityDefinition.GetAttributeDefinitions() as IEnumerable<IAttributeDefinition>;

                // Ouput Attributes
                schemaFile.WriteLine("type " + entityDefinition.Name + "ATTR {");
                OutputAttribute(attributeDefinitionList, entityDefinition.Name, true);
                // Ouput Attributes as input for mutations
                schemaFile.WriteLine("# for mutations");
                schemaFile.WriteLine("input " + entityDefinition.Name + "ATTR_Input {");
                OutputAttribute(attributeDefinitionList, entityDefinition.Name, false);

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

                    // Check if at least one of the Targets is part of the selected "Facility"
                    IEnumerable<IEntityDefinition> targetEntityDefinitionList =
                        entityDefinition.GetTargetEntityDefinitions(relationDefinition);
                    Boolean found = false;
                    foreach (IEntityDefinition targetEntityDefinition in targetEntityDefinitionList)
                    {
                        if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == true)
                    {   // at least one Target found in 'Facility'
                        List<string> commentLine = WrapLineComment(relationDefinition.Description.ToString());
                        foreach (string line in commentLine)
                        {
                            // Relation description
                            schemaFile.WriteLine("  " + line);
                        }
                        schemaFile.WriteLine("  " + camelRelationName + ": [" +
                            Char.ToUpper(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) + "_" +
                            Char.ToUpper(camelRelationName[0]) +
                            camelRelationName.Substring(1) + "Target]");

                        schemaFile.WriteLine(""); // space between relationships
                    }
                }
                schemaFile.WriteLine("}");
                // output relation input for mutations
                schemaFile.WriteLine("# for mutations");
                schemaFile.WriteLine("input " + entityDefinition.Name + "REL_Input {");
                foreach (String relationDefinitionName in sortedEntityRelationList)
                {
                    String camelRelationName = GetCamelCaseRelation(relationDefinitionName);

                    IRelationDefinition relationDefinition = schema.GetRelationDefinition(relationDefinitionName);

                    // Check if at least one of the Targets is part of the selected "Facility"
                    IEnumerable<IEntityDefinition> targetEntityDefinitionList =
                        entityDefinition.GetTargetEntityDefinitions(relationDefinition);
                    Boolean found = false;
                    foreach (IEntityDefinition targetEntityDefinition in targetEntityDefinitionList)
                    {
                        if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == true)
                    {   // at least one Target found in 'Facility'
                        schemaFile.WriteLine("  " + camelRelationName + ": [" +
                            Char.ToUpper(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) + "_" +
                            Char.ToUpper(camelRelationName[0]) +
                            camelRelationName.Substring(1) + "Target_Input]");
                    }
                }
                schemaFile.WriteLine("}");
                // Output Relationship Target
                foreach (String relationDefinitionName in sortedEntityRelationList)
                {
                    String camelRelationName = GetCamelCaseRelation(relationDefinitionName);
                    IRelationDefinition relationDefinition = schema.GetRelationDefinition(relationDefinitionName);
                    IEnumerable<IEntityDefinition> targetEntityDefinitionList =
                        entityDefinition.GetTargetEntityDefinitions(relationDefinition);
                    Boolean found = false;
                    foreach (IEntityDefinition targetEntityDefinition in targetEntityDefinitionList)
                    {
                        if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == true)
                    {   // at least one Target found in 'Facility'

                        schemaFile.WriteLine("type " +
                        Char.ToUpper(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) + "_" +
                        Char.ToUpper(camelRelationName[0]) +
                        camelRelationName.Substring(1) + "Target {");

                        foreach (IEntityDefinition targetEntityDefinition in targetEntityDefinitionList)
                        {
                            if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                            {
                                // Only include Target if included in selected "Facility"
                                schemaFile.WriteLine("  " + Char.ToLower(targetEntityDefinition.Name[0]) +
                                    targetEntityDefinition.Name.Substring(1) + "Target: " +
                                    Char.ToUpper(targetEntityDefinition.Name[0]) +
                                    targetEntityDefinition.Name.Substring(1) + "ID");
                            }
                        }

                        IEnumerable<IAttributeDefinition> relAttributeDefinitionList = relationDefinition.GetAttributeDefinitions() as IEnumerable<IAttributeDefinition>;
                        // Ouput Relationship Attributes
                        string camelCaseRelationName = GetCamelCaseRelation(relationDefinition.Name);

                        // Relationship Attribute Type name is: <Component>_<Relationship><EnumName>
                        OutputAttribute(relAttributeDefinitionList, entityDefinition.Name + "_" +
                            Char.ToUpper(camelCaseRelationName[0]) + camelCaseRelationName.Substring(1), true);
                    }
                }
                // Output Relationship Target as input for mutation
                schemaFile.WriteLine("# for mutations");
                foreach (String relationDefinitionName in sortedEntityRelationList)
                {
                    String camelRelationName = GetCamelCaseRelation(relationDefinitionName);
                    IRelationDefinition relationDefinition = schema.GetRelationDefinition(relationDefinitionName);
                    IEnumerable<IEntityDefinition> targetEntityDefinitionList =
                        entityDefinition.GetTargetEntityDefinitions(relationDefinition);
                    Boolean found = false;
                    foreach (IEntityDefinition targetEntityDefinition in targetEntityDefinitionList)
                    {
                        if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == true)
                    {   // at least one Target found in 'Facility'

                        schemaFile.WriteLine("input " +
                        Char.ToUpper(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) + "_" +
                        Char.ToUpper(camelRelationName[0]) +
                        camelRelationName.Substring(1) + "Target_Input {");

                        schemaFile.WriteLine("  operation: MutationOperation!");

                        foreach (IEntityDefinition targetEntityDefinition in targetEntityDefinitionList)
                        {
                            if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                            {
                                // Only include Target if included in selected "Facility"
                                schemaFile.WriteLine("  " + Char.ToLower(targetEntityDefinition.Name[0]) +
                                    targetEntityDefinition.Name.Substring(1) + "Target: " +
                                    Char.ToUpper(targetEntityDefinition.Name[0]) +
                                    targetEntityDefinition.Name.Substring(1) + "ID_Input");
                            }
                        }

                        IEnumerable<IAttributeDefinition> relAttributeDefinitionList = relationDefinition.GetAttributeDefinitions() as IEnumerable<IAttributeDefinition>;
                        // Ouput Relationship Attributes
                        string camelCaseRelationName = GetCamelCaseRelation(relationDefinition.Name);

                        // Relationship Attribute Type name is: <Component>_<Relationship><EnumName>
                        OutputAttribute(relAttributeDefinitionList, entityDefinition.Name + "_" +
                            Char.ToUpper(camelCaseRelationName[0]) + camelCaseRelationName.Substring(1), false);
                    }
                }
            }
            // Output the function call structure schema
            schemaFile.WriteLine("type CallStructure {");
            schemaFile.WriteLine("  function: FunctionID");
            schemaFile.WriteLine("  structure: StructureItem");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type StructureItem {");
            schemaFile.WriteLine("  id: ID!");
            schemaFile.WriteLine("  type: StructureType");
            schemaFile.WriteLine("  # optional annotation for a Branch");
            schemaFile.WriteLine("  annotation: String");
            schemaFile.WriteLine("  # reference UUID / Name / Num for: Function, Exit / ExitCondition (Exit), Replicate (DomainSet) types");
            schemaFile.WriteLine("  referenceID: String");
            schemaFile.WriteLine("  referenceName: String");
            schemaFile.WriteLine("  referenceNum: String");
            schemaFile.WriteLine("  structure: [StructureItem]");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("enum StructureType");
            schemaFile.WriteLine("{");
            schemaFile.WriteLine("  Branch");
            schemaFile.WriteLine("  Function");
            schemaFile.WriteLine("  Exit");
            schemaFile.WriteLine("  ExitCondition");
            schemaFile.WriteLine("  Loop");
            schemaFile.WriteLine("  LoopExit");
            schemaFile.WriteLine("  Parallel");
            schemaFile.WriteLine("  Replicate");
            schemaFile.WriteLine("  Select");
            schemaFile.WriteLine("}");
        }
        static void CreateModel(string projectName, string outFileName)
        {
            var project = Connect(projectName);
            // Write Project header
            dataFile = new System.IO.StreamWriter(@"..\..\..\sample\" + outFileName, false);
            dataFile.WriteLine("".PadLeft(indent) + "{");
            indent += 2;
            dataFile.WriteLine("".PadLeft(indent) + @"""data"": {");
            indent += 2;
            dataFile.WriteLine("".PadLeft(indent) + @"""cpsSystemModel"": {");
            indent += 2;
            dataFile.WriteLine("".PadLeft(indent) + @"""project"": {");
            indent += 2;
            dataFile.WriteLine("".PadLeft(indent) + @"""id"": """ + project.Id.ToString() + @""",");
            dataFile.WriteLine("".PadLeft(indent) + @"""name"": """ + project.Name + @""",");
            dataFile.WriteLine("".PadLeft(indent) + @"""description"": """ +
                (project.Description?.PlainText.Replace(Environment.NewLine, "\n") ?? null) + @""",");
            dataFile.WriteLine("".PadLeft(indent) + @"""version"": """ + (project.Version?.ToString() ?? null) + @"""");
            indent -= 2;
            dataFile.WriteLine("".PadLeft(indent) + "},");

            foreach (String entityType in sortedEntityDefinitionList)
            {
                String entityAlias = schema.GetEntityDefinition(entityType).Alias ?? entityType;
                IFolder folder = project.GetFolder(entityAlias);


                // Create a sorted list of Relations for EntityType
                IEnumerable<IRelationDefinition> entityRelationDefinitionList =
                    schema.GetEntityDefinition(entityType).GetRelationDefinitions();
                List<String> sortedEntityRelationList = new List<String>();
                foreach (IRelationDefinition entityRelationDefinition in entityRelationDefinitionList)
                {
                    sortedEntityRelationList.Add(entityRelationDefinition.Name);
                }
                sortedEntityRelationList.Sort();

                // Output Entity Type
                if (folder.EntityCount == 0)
                {
                    dataFile.WriteLine("".PadLeft(indent) + @"""" + LowerFirst(entityType) + @""": [],");
                }
                else
                {
                    dataFile.WriteLine("".PadLeft(indent) + @"""" + LowerFirst(entityType) + @""": [");
                    indent += 2;
                    IEnumerable<IEntity> entityList = folder.GetAllEntities();
                    int currentEntity = 0;
                    int totalEntity = folder.EntityCount;
                    ISortBlock numericSortBlock = project.GetSortBlock(SortBlockConstants.Numeric);
                    // Output Entity Instance
                    foreach (IEntity entity in numericSortBlock.SortEntities(entityList))
                    {
                        currentEntity += 1;
                        // Ouput identity
                        dataFile.WriteLine("".PadLeft(indent) + "{");
                        indent += 2;
                        dataFile.WriteLine("".PadLeft(indent) + @"""identity"": {");
                        indent += 2;
                        dataFile.WriteLine("".PadLeft(indent) + @"""id"": """ + entity.Id.ToString() + @""",");
                        dataFile.WriteLine("".PadLeft(indent) + @"""name"": """ + (entity?.Name ?? null) + @""",");
                        dataFile.WriteLine("".PadLeft(indent) + @"""number"": """ +
                            (entity.GetAttribute("number")?.ToString() ?? null) + @"""");
                        indent -= 2;
                        dataFile.WriteLine("".PadLeft(indent) + "},");

                        // Output attributes
                        dataFile.WriteLine("".PadLeft(indent) + @"""attributes"": {");
                        indent += 2;
                        IEnumerable<IAttributeValue> attributeList = entity.Attributes as IEnumerable<IAttributeValue>;
                        GenesysQuery.dataFileLine = "";
                        OutputAttributeValue(attributeList);
                        dataFile.WriteLine(GenesysQuery.dataFileLine);
                        indent -= 2;
                        dataFile.WriteLine("".PadLeft(indent) + "},");

                        // Output params
                        dataFileLine = "";
                        dataFileLine += "".PadLeft(indent) + @"""parameters"": [";
                        indent += 2;
                        IEnumerable<IEntityParameterValue> parameterList = entity.Parameters;
                        int paramCount = 0;
                        if (parameterList.Any())
                        {
                            foreach (IEntityParameterValue parameter in parameterList)
                            {
                                paramCount++;
                                if (paramCount > 1)
                                {
                                    dataFileLine += @",";
                                }
                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"{";
                                indent += 2;
                                dataFileLine += Environment.NewLine + "".PadLeft(indent) +
                                    @"""name"": """ + parameter.DisplayName + @""",";

                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""description"": ";
                                dataFileLine += parameter.AttributeDefinition.Description != null ?
                                    @"""" + parameter.AttributeDefinition.Description.
                                        ToString().Replace(Environment.NewLine, "::") + @"""," : "null,";

                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""objective"": ";
                                dataFileLine += parameter.Objective != null ?
                                     @"""" + parameter.Objective.ToString() + @"""," : "null,";
  
                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""threshold"": ";
                                dataFileLine += parameter.Threshold != null ?
                                     @"""" + parameter.Threshold.ToString() + @"""," : "null,";

                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""design"": ";
                                dataFileLine += parameter.Design != null ?
                                    @"""" + parameter.Design.ToString() + @"""," : "null,";

                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""observed"": ";
                                dataFileLine += parameter.Observed != null ?
                                    @"""" + parameter.Observed.ToString() + @"""," : "null,";

                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""units"": ";
                                dataFileLine += parameter.Units != null ?
                                    @"""" + parameter.Units.ToString() + @"""" : "null";
                                
                                indent -= 2;
                                dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"}";
                            }
                        }
                        indent -= 2;
                        if (paramCount > 0)
                        {
                            dataFileLine += Environment.NewLine + "".PadLeft(indent) + "],";
                        }
                        else
                        {   // no params
                            dataFileLine += "],";
                        }
                        dataFile.WriteLine(dataFileLine);

                        // Output relations
                        dataFile.WriteLine("".PadLeft(indent) + @"""relations"": {");
                        indent += 2;

                        int relCount = 0;
                        dataFileLine = "";
                        foreach (String relation in sortedEntityRelationList)
                        {
                            IRelationDefinition relationDefinition = schema.GetRelationDefinition(relation);

                            // Check if at least one of the Targets is part of the selected "Facility"
                            IEnumerable<IEntityDefinition> targetEntityDefinitionList =
                                entity.GetEntityDefinition().GetTargetEntityDefinitions(relationDefinition);
                            Boolean found = false;
                            foreach (IEntityDefinition targetEntityDefinition in targetEntityDefinitionList)
                            {
                                if (sortedEntityDefinitionList.Contains(targetEntityDefinition.Name))
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (found == true)
                            {
                                relCount++;
                                if (relCount > 1)
                                {
                                    dataFileLine += @",";
                                    dataFile.WriteLine(dataFileLine);
                                    dataFileLine = "";
                                }
                                dataFileLine += "".PadLeft(indent) + @"""" + GetCamelCaseRelation(relation) + @""": [";
                                int relTargetCount = 0;
                                indent += 2;

                                //foreach (IEntity relationTarget in entity.GetRelationshipTargets(relation))
                                foreach (IRelationship relationship in entity.GetRelationships(relation))
                                {
                                    IEntity relationTarget = relationship.GetTarget();
                                    if (sortedEntityDefinitionList.Contains(relationTarget.GetEntityDefinition().Name))
                                    {
                                        // Only include Target EntityType if included in selected "Facility"
                                        relTargetCount++;
                                        if (relTargetCount > 1)
                                        {
                                            dataFileLine += @",";
                                        }
                                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + "{";
                                        indent += 2;
                                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""" +
                                            LowerFirst(relationTarget.GetEntityDefinition().Name) + @"Target"": {";
                                        indent += 2;
                                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""id"": """ + relationTarget.Id + @""",";
                                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""name"": """ + relationTarget.Name + @""",";
                                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""number"": """ +
                                            relationTarget.GetAttributeValueString("number") + @"""";
                                        indent -= 2;
                                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + "}";

                                        // Ouput Relationship Attributes
                                        IEnumerable<IAttributeValue> relationAttributeList = relationship.GetAttributes();
                                        if (relationAttributeList.Any())
                                        {
                                            dataFileLine += "," + Environment.NewLine;
                                            OutputAttributeValue(relationAttributeList);
                                        }

                                        indent -= 2;
                                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + "}";
                                    }

                                }
                               
                                indent -= 2;
                                if (relTargetCount > 0)
                                {
                                    dataFileLine += Environment.NewLine + "".PadLeft(indent) + "]";
                                }
                                else
                                {   // no targets
                                    dataFileLine += "]";
                                }
                            }
                        }
                        if (relCount > 0)
                        {
                            dataFile.WriteLine(dataFileLine);
                        }
                        indent -= 2;
                        dataFile.WriteLine("".PadLeft(indent) + "}");

                        indent -= 2;
                        if (currentEntity == totalEntity)
                        {   // last in list - no ,
                            dataFile.WriteLine("".PadLeft(indent) + "}");
                        }
                        else
                        {
                            dataFile.WriteLine("".PadLeft(indent) + "},");
                        }
                    }
                    indent -= 2;
                    dataFile.WriteLine("".PadLeft(indent) + "],");
                }
            }
            CreateStructure(project);
            indent -= 2;  // missionAware: close
            dataFile.WriteLine("".PadLeft(indent) + "}");
            indent -= 2; // data: close
            dataFile.WriteLine("".PadLeft(indent) + "}");
            indent -= 2; // file: close
            dataFile.WriteLine("".PadLeft(indent) + "}");
        }

        // Output Attribute Values (Entity or Relationship)
        static void OutputAttributeValue(IEnumerable<IAttributeValue> attributeList)
        {
            int attrCount = 0;
            foreach (IAttributeValue attribute in attributeList)
            {
                if (attribute.AttributeDefinition.Name == "name" ||
                        attribute.AttributeDefinition.Name == "number")
                {
                    continue; // name and number are part of "identity"
                }
                DataTypeDefinition attributeType = attribute.AttributeDefinition.DataType;
                if (attributeType.ToString() == "Vitech.Genesys.Common.ScriptSpecTypeDefinition")
                {
                    continue; // Skip script attributes
                }
                attrCount++;
                if (attrCount > 1)
                {
                    dataFileLine += @",";
                    dataFileLine += Environment.NewLine;
                }
                string attributeName = attribute.AttributeDefinition.Name.Replace("-", "_");
                if (attributeType.ToString() == "Vitech.Genesys.Common.BooleanTypeDefinition")
                {
                    if (attribute.GetValueString() == "True")
                    {
                        dataFileLine += "".PadLeft(indent) + @"""" + attributeName +
                            @""": " + "true";
                    }
                    else
                    {
                        dataFileLine += "".PadLeft(indent) + @"""" + attributeName +
                            @""": " + "false";
                    }

                }
                else if (attributeType.ToString() == "Vitech.Genesys.Common.FloatTypeDefinition" ||
                            attributeType.ToString() == "Vitech.Genesys.Common.IntegerTypeDefinition")
                {
                    dataFileLine += "".PadLeft(indent) + @"""" + attributeName +
                        @""": " + (attribute.GetValue() ?? "null");
                }
                else if (attributeType.ToString() == "Vitech.Genesys.Common.EnumerationTypeDefinition")
                {
                    dataFileLine += "".PadLeft(indent) + @"""" + attributeName +
                    @""": """ + AdjustEnumValue(attribute.GetValueString()) + @"""";
                }
                else if (attributeType.ToString() == "Vitech.Genesys.Common.CollectionTypeDefinition")
                {
                    dataFileLine += "".PadLeft(indent) + @"""" + attributeName + @""": [";
                    indent += 2;

                    if (attribute.GetValue() is Array attrSet)
                    {
                        bool firstAttrInSet = true;
                        foreach (var attr in attrSet)
                        {
                            if (firstAttrInSet != true)
                            {
                                dataFileLine += @",";
                            }
                            firstAttrInSet = false;
                            dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"""" + attr + @"""";
                        }
                        indent -= 2;
                        dataFileLine += Environment.NewLine + "".PadLeft(indent) + @"]";
                    }
                    else
                    {
                        dataFileLine += @"]";
                        indent -= 2;
                    }

                }
                else if (attributeType.ToString() == "Vitech.Genesys.Common.FormattedTextTypeDefinition")
                {
                    if (!(attribute?.GetValue() is Text attributeValue))
                    {
                        dataFileLine += "".PadLeft(indent) + @"""" + attributeName + @""": " + "null";
                    }
                    else
                    {
                        String attributeString = attributeValue?.PlainText.Replace(Environment.NewLine, "::") ?? null;
                        dataFileLine += "".PadLeft(indent) + @"""" + attributeName +
                            @""": """ + attributeString + @"""";
                    }
                }
                else
                {
                    // All other are String
                    if (attribute.GetValueString() == "")
                    {
                        dataFileLine += "".PadLeft(indent) + @"""" + attributeName + @""": " + "null";
                    }
                    else
                    {
                        dataFileLine += "".PadLeft(indent) + @"""" + attributeName +
                            @""": """ + attribute.GetValueString() + @"""";
                    }
                }
            }
        }
        // Output Attributes (Entity or Relationship)
        static void OutputAttribute(IEnumerable<IAttributeDefinition> attributeDefinitionList, string ownerName, bool fullDefinition)
        {
            foreach (IAttributeDefinition attributeDefinition in attributeDefinitionList)
            {
                if (attributeDefinition.Name == "name" || attributeDefinition.Name == "number")
                {
                    continue; // name and number are part of "identity"
                }
                DataTypeDefinition entityAttributeType = attributeDefinition.DataType;
                if (entityAttributeType.ToString() == "Vitech.Genesys.Common.ScriptSpecTypeDefinition")
                {
                    continue; // Skip script attributes
                }
                if (fullDefinition == true)
                {
                    List<string> commentLine = WrapLineComment(attributeDefinition.Description.ToString());
                    foreach (string line in commentLine)
                    {
                        // Entity description
                        schemaFile.WriteLine("  " + line);
                    }
                }

                string attributeDefinitionName = attributeDefinition.Name.Replace("-", "_");

                switch (entityAttributeType.ToString())
                {
                    case "Vitech.Genesys.Common.FormattedTextTypeDefinition":
                    case "Vitech.Genesys.Common.NumberSpecTypeDefinition":
                    case "Vitech.Genesys.Common.ReferenceSpecTypeDefinition":
                    case "Vitech.Genesys.Common.StringTypeDefinition":
                    case "Vitech.Genesys.Common.DateTimeTypeDefinition":
                    case "Vitech.Genesys.Common.DateTypeDefinition":
                    case "Vitech.Genesys.Common.HierarchicalNumberTypeDefinition":
                        schemaFile.WriteLine("  " + attributeDefinitionName + ": String");
                        break;
                    case "Vitech.Genesys.Common.BooleanTypeDefinition":
                        schemaFile.WriteLine("  " + attributeDefinitionName + ": Boolean");
                        break;
                    case "Vitech.Genesys.Common.FloatTypeDefinition":
                        schemaFile.WriteLine("  " + attributeDefinitionName + ": Float");
                        break;
                    case "Vitech.Genesys.Common.EnumerationTypeDefinition":
                        EnumerationTypeDefinition enumDefinition = attributeDefinition.DataType as EnumerationTypeDefinition;
                        var capEnumAttributeName = Char.ToUpper(attributeDefinitionName[0]) +
                            attributeDefinitionName.Substring(1);
                        // Enum type is: <OwnerName><AttributeName> 
                        schemaFile.WriteLine("  " + attributeDefinitionName + ": " + ownerName + capEnumAttributeName);
                        break;
                    case "Vitech.Genesys.Common.IntegerTypeDefinition":
                        schemaFile.WriteLine("  " + attributeDefinitionName + ": Int");
                        break;
                    case "Vitech.Genesys.Common.CollectionTypeDefinition":
                        CollectionTypeDefinition collectionDefinition = attributeDefinition.DataType as CollectionTypeDefinition;
                        switch (collectionDefinition.ValueType.ToString())
                        {
                            case "Vitech.Genesys.Common.StringTypeDefinition":
                                schemaFile.WriteLine("  " + attributeDefinitionName + ": [String]");
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
                if (fullDefinition == true)
                {
                    schemaFile.WriteLine(""); // leave space between attribute definitions
                }

            }
            schemaFile.WriteLine("}");

            if (fullDefinition == true)
            {
                // Ouput Enum types
                foreach (IAttributeDefinition attributeDefinition in attributeDefinitionList)
                {
                    DataTypeDefinition attributeType = attributeDefinition.DataType;
                    if (attributeType.ToString() == "Vitech.Genesys.Common.EnumerationTypeDefinition")
                    {
                        var capEnumAttributeName = Char.ToUpper(attributeDefinition.Name[0]) +
                            attributeDefinition.Name.Substring(1);
                        schemaFile.WriteLine("enum " + ownerName + capEnumAttributeName.Replace("-", "_") + " {");

                        EnumerationTypeDefinition enumDefinition = attributeDefinition.DataType as EnumerationTypeDefinition;
                        EnumPossibleValue[] enumPossibleValues = enumDefinition.PossibleValues;
                        for (var i = 0; i < enumPossibleValues.Length; i++)
                        {
                            String enumValue = AdjustEnumValue(enumPossibleValues[i].ToString());
                            schemaFile.WriteLine("  " + enumValue);
                        }
                        schemaFile.WriteLine("}");
                    }
                }
            }
        }
        // Adjust Enum value to remove sepcial characters and spaces
        static string AdjustEnumValue(string input)
        {
            String enumValue = input.Replace("/", "_").
                           Replace(" ", "_").Replace("-", "").Replace("&", "").
                           Replace(":", "").Replace("(", "").Replace(")", "");
            if (Char.IsDigit(enumValue[0]))
            {
                // Enum canot begin with a digit - prepend "E_"
                enumValue = "E_" + enumValue;
            }
            return enumValue;
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
                    camelRelationName += Char.ToUpper(relationNameParts[i][0]) +
                        relationNameParts[i].Substring(1);
                }
            }
            return camelRelationName;
        }
        static string LowerFirst(string input)
        {
            return Char.ToLower(input[0]) + input.Substring(1);
        }

        // Wrap multiline comments
        static List<string> WrapLineComment(string text)
        {
            int start = 0, margin = 80, end;
            var lines = new List<string>();
            text = Regex.Replace(text, @"\s", " ").Trim();
            // replace any \ in comments with /
            text = Regex.Replace(text, @"\\", "/").Trim();

            if (text.Length > 0)
            {
                lines.Add(@"""""""");
                while ((end = start + margin) < text.Length)
                {
                    while (text[end] != ' ' && end > start)
                        end -= 1;

                    if (end == start)
                        end = start + margin;

                    lines.Add(text.Substring(start, end - start));
                    start = end + 1;
                }

                if (start < text.Length)
                    lines.Add(text.Substring(start));

                lines.Add(@"""""""");

            }
            return lines;
        }
    }
}