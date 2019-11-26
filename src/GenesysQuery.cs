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
        static System.IO.StreamWriter dataFile;
        static int indent = 0;
        static List<String> sortedEntityDefinitionList = new List<String>();
        static ISchema schema;
        static string facilityName;

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
            IFacility facility = schema.GetFacility(new Guid("da424ed7-b58a-4496-af10-35adf696efd1")); // SE GUID
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
            }
            sortedEntityDefinitionList.Sort();

            return project;
        }
        static void CreateSchema(string projectName)
        {
            var project = Connect(projectName);

            // Write GraphQL Schema header
            schemaFile = new System.IO.StreamWriter(@"..\..\..\schema\ma-meta-model.graphql", false);
            schemaFile.WriteLine("schema {");
            schemaFile.WriteLine("  query: Query");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type Query {");
            schemaFile.WriteLine("  # System Model for: '" + facilityName + "' Facility");
            schemaFile.WriteLine("  missionAwareSystemModel: MissionAwareSystemModel");
            schemaFile.WriteLine("}");
            schemaFile.WriteLine("type MissionAwareSystemModel {");
            schemaFile.WriteLine("  # The project identity.");
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
                // lowercase first character of Component and make plural variable name
                schemaFile.WriteLine("  " + Char.ToLower(entityDefinition.Name[0]) + entityDefinition.Name.Substring(1) +
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

            // Ouput Parameter Definition
            schemaFile.WriteLine("type Parameter {");
            schemaFile.WriteLine("  name: String!");
            schemaFile.WriteLine("  description: String");
            schemaFile.WriteLine("  type: String");
            schemaFile.WriteLine("  objectvie: String");
            schemaFile.WriteLine("  threshold: String");
            schemaFile.WriteLine("  design: String");
            schemaFile.WriteLine("  observed: String");
            schemaFile.WriteLine("  units: String");
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
                schemaFile.WriteLine("  parameters: [Parameter]");
                schemaFile.WriteLine("  relations: " + entityDefinition.Name + "REL");
                schemaFile.WriteLine("}");

                // Output entity identity
                schemaFile.WriteLine("type " + entityDefinition.Name + "ID {");
                schemaFile.WriteLine("  id: ID!");
                schemaFile.WriteLine("  name: String!");
                schemaFile.WriteLine("  number: String!");
                schemaFile.WriteLine("}");

                IEnumerable<IAttributeDefinition> attributeDefinitionList = entityDefinition.GetAttributeDefinitions() as IEnumerable<IAttributeDefinition>;

                // Ouput Attributes
                schemaFile.WriteLine("type " + entityDefinition.Name + "ATTR {");
                OutputAttribute(attributeDefinitionList, entityDefinition.Name);

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
                            Char.ToUpper(camelCaseRelationName[0]) + camelCaseRelationName.Substring(1));
                    }
                }
            }
        }
        static void CreateModel(string projectName, string outFileName)
        {
            var project = Connect(projectName);
            // Write Project header
            dataFile = new System.IO.StreamWriter(@"..\..\..\sample\ma.json", false);
            dataFile.WriteLine("".PadLeft(indent) + "{");
            indent += 2;
            dataFile.WriteLine("".PadLeft(indent) + @"""data"": {");
            indent += 2;
            dataFile.WriteLine("".PadLeft(indent) + @"""missionAwareSystemModel"": {");
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

            int currentEntityType = 0;
            int totalEntityType = sortedEntityDefinitionList.Count;

            foreach (String entityType in sortedEntityDefinitionList)
            {
                String entityAlias = schema.GetEntityDefinition(entityType).Alias ?? entityType;
                IFolder folder = project.GetFolder(entityAlias);
                currentEntityType += 1;

                // Output Entity Type
                if (folder.EntityCount == 0)
                {
                    if (currentEntityType == totalEntityType)
                    {   // last in list - no ,
                        dataFile.WriteLine("".PadLeft(indent) + @"""" + LowerFirst(entityType) + @""": []");
                    }
                    else
                    {
                        dataFile.WriteLine("".PadLeft(indent) + @"""" + LowerFirst(entityType) + @""": [],");
                    }
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
                        int attrCount = 0;
                        string attributeLine = "";
                        IEntityAttributeList attributeList = entity.Attributes;
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
                                attributeLine += @",";
                                dataFile.WriteLine(attributeLine);
                                attributeLine = "";
                            }
                            string attributeName = attribute.AttributeDefinition.Name.Replace("-", "_");
                            if (attributeType.ToString() == "Vitech.Genesys.Common.BooleanTypeDefinition")
                            {
                                if (attribute.GetValueString() == "True")
                                {
                                    attributeLine = "".PadLeft(indent) + @"""" + attributeName +
                                        @""": " + "true";
                                }
                                else
                                {
                                    attributeLine = "".PadLeft(indent) + @"""" + attributeName +
                                        @""": " + "false";
                                }

                            }
                            else if (attributeType.ToString() == "Vitech.Genesys.Common.FloatTypeDefinition" ||
                                        attributeType.ToString() == "Vitech.Genesys.Common.IntegerTypeDefinition")
                            {
                                attributeLine = "".PadLeft(indent) + @"""" + attributeName +
                                    @""": " + (attribute.GetValue() ?? "null");
                            }
                            else if (attributeType.ToString() == "Vitech.Genesys.Common.EnumerationTypeDefinition")
                            {
                                attributeLine = "".PadLeft(indent) + @"""" + attributeName +
                                @""": """ + AdjustEnumValue(attribute.GetValueString()) + @"""";
                            }
                            else if (attributeType.ToString() == "Vitech.Genesys.Common.CollectionTypeDefinition")
                            {
                                attributeLine = "".PadLeft(indent) + @"""" + attributeName + @""": [";
                                indent += 2;

                                if (attribute.GetValue() is Array attrSet)
                                {
                                    bool firstAttrInSet = true;
                                    foreach (var attr in attrSet)
                                    {
                                        if (firstAttrInSet != true)
                                        {
                                            attributeLine += @",";
                                        }
                                        firstAttrInSet = false;
                                        attributeLine += Environment.NewLine + "".PadLeft(indent) + @"""" + attr + @"""";
                                    }
                                    indent -= 2;
                                    attributeLine += Environment.NewLine + "".PadLeft(indent) + @"]";
                                }
                                else
                                {
                                    attributeLine += @"]";
                                    indent -= 2;
                                }

                            }
                            else if (attributeType.ToString() == "Vitech.Genesys.Common.FormattedTextTypeDefinition")
                            {
                                if (!(attribute?.GetValue() is Text attributeValue))
                                {
                                    attributeLine = "".PadLeft(indent) + @"""" + attributeName + @""": " + "null";
                                }
                                else
                                {
                                    String attributeString = attributeValue?.PlainText.Replace(Environment.NewLine, "::") ?? null;
                                    attributeLine = "".PadLeft(indent) + @"""" + attributeName +
                                        @""": """ + attributeString + @"""";
                                }
                            }
                            else
                            {
                                // All other are String
                                if (attribute.GetValueString() == "")
                                {
                                    attributeLine = "".PadLeft(indent) + @"""" + attributeName + @""": " + "null";
                                }
                                else
                                {
                                    attributeLine = "".PadLeft(indent) + @"""" + attributeName +
                                        @""": """ + attribute.GetValueString() + @"""";
                                }
                            }
                        }
                        if (attrCount > 0)
                        {
                            dataFile.WriteLine(attributeLine);
                        }
                        indent -= 2;
                        dataFile.WriteLine("".PadLeft(indent) + "},");
                        // Output params
                        dataFile.WriteLine("".PadLeft(indent) + @"""parameters"": {");
                        indent += 2;

                        indent -= 2;
                        dataFile.WriteLine("".PadLeft(indent) + "},");
                        // Output relations
                        dataFile.WriteLine("".PadLeft(indent) + @"""relations"": {");
                        indent += 2;

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
                    if (currentEntityType == totalEntityType)
                    {   // last in list - no ,
                        dataFile.WriteLine("".PadLeft(indent) + "]");
                    }
                    else
                    {
                        dataFile.WriteLine("".PadLeft(indent) + "],");
                    }
                }
            }
            indent -= 2;  // missionAware: close
            dataFile.WriteLine("".PadLeft(indent) + "}");
            indent -= 2; // data: close
            dataFile.WriteLine("".PadLeft(indent) + "}");
            indent -= 2; // file: close
            dataFile.WriteLine("".PadLeft(indent) + "}");
        }

        // Output Attributes (Entity or Relationship)
        static void OutputAttribute(IEnumerable<IAttributeDefinition> attributeDefinitionList, string ownerName)
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
                List<string> commentLine = WrapLineComment(attributeDefinition.Description.ToString());
                foreach (string line in commentLine)
                {
                    // Entity description
                    schemaFile.WriteLine("  " + line);
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
                schemaFile.WriteLine(""); // leave space between attribute definitions

            }
            schemaFile.WriteLine("}");

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