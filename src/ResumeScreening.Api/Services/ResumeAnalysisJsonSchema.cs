using System.Text.Json;

namespace ResumeScreening.Api.Services;

internal static class ResumeAnalysisJsonSchema
{
    public static JsonElement Value { get; } = Parse();

    private static JsonElement Parse()
    {
        using var document = JsonDocument.Parse(SchemaJson);
        return document.RootElement.Clone();
    }

    private const string SchemaJson = """
        {
          "type": "object",
          "properties": {
            "candidate": {
              "type": "object",
              "properties": {
                "name": { "$ref": "#/$defs/evidenceBackedText" },
                "email": { "$ref": "#/$defs/evidenceBackedText" },
                "phone": { "$ref": "#/$defs/evidenceBackedText" },
                "age": { "$ref": "#/$defs/evidenceBackedText" },
                "gender": { "$ref": "#/$defs/evidenceBackedText" },
                "institution": { "$ref": "#/$defs/evidenceBackedText" },
                "educationLevel": { "$ref": "#/$defs/evidenceBackedText" },
                "major": { "$ref": "#/$defs/evidenceBackedText" }
              },
              "required": [
                "name", "email", "phone", "age", "gender", "institution",
                "educationLevel", "major"
              ],
              "additionalProperties": false
            },
            "educations": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "institution": { "type": ["string", "null"] },
                  "degree": { "type": ["string", "null"] },
                  "major": { "type": ["string", "null"] },
                  "startDate": { "type": ["string", "null"] },
                  "endDate": { "type": ["string", "null"] },
                  "evidence": { "$ref": "#/$defs/evidenceArray" }
                },
                "required": [
                  "institution", "degree", "major", "startDate", "endDate", "evidence"
                ],
                "additionalProperties": false
              }
            },
            "workExperiences": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "employer": { "type": ["string", "null"] },
                  "jobTitle": { "type": ["string", "null"] },
                  "startDate": { "type": ["string", "null"] },
                  "endDate": { "type": ["string", "null"] },
                  "responsibilities": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "evidence": { "$ref": "#/$defs/evidenceArray" }
                },
                "required": [
                  "employer", "jobTitle", "startDate", "endDate",
                  "responsibilities", "evidence"
                ],
                "additionalProperties": false
              }
            },
            "projects": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "projectName": { "type": ["string", "null"] },
                  "role": { "type": ["string", "null"] },
                  "description": { "type": ["string", "null"] },
                  "technologies": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "evidence": { "$ref": "#/$defs/evidenceArray" }
                },
                "required": [
                  "projectName", "role", "description", "technologies", "evidence"
                ],
                "additionalProperties": false
              }
            },
            "skills": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "context": { "type": ["string", "null"] },
                  "evidence": { "$ref": "#/$defs/evidenceArray" }
                },
                "required": ["name", "context", "evidence"],
                "additionalProperties": false
              }
            },
            "findings": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "kind": {
                    "type": "string",
                    "enum": ["Strength", "Concern", "NeedsVerification"]
                  },
                  "conclusion": { "type": "string" },
                  "evidence": { "$ref": "#/$defs/evidenceArray" }
                },
                "required": ["kind", "conclusion", "evidence"],
                "additionalProperties": false
              }
            },
            "missingInformation": {
              "type": "array",
              "items": { "type": "string" }
            }
          },
          "required": [
            "candidate", "educations", "workExperiences", "projects",
            "skills", "findings", "missingInformation"
          ],
          "additionalProperties": false,
          "$defs": {
            "evidenceBackedText": {
              "type": "object",
              "properties": {
                "value": { "type": ["string", "null"] },
                "evidence": { "$ref": "#/$defs/evidenceArray" }
              },
              "required": ["value", "evidence"],
              "additionalProperties": false
            },
            "evidenceArray": {
              "type": "array",
              "items": { "$ref": "#/$defs/evidence" }
            },
            "evidence": {
              "type": "object",
              "properties": {
                "pageNumber": { "type": "integer" },
                "quote": { "type": "string" }
              },
              "required": ["pageNumber", "quote"],
              "additionalProperties": false
            }
          }
        }
        """;
}
