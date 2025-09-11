Imports System.Text.Json.Serialization

Public Class HDiffMap
    <JsonPropertyName("diff_map")>
    Public Property DiffMap As List(Of HDiffData)
End Class

Public Class HDiffData
    <JsonPropertyName("source_file_name")>
    Public Property SourceFileName As String

    <JsonPropertyName("target_file_name")>
    Public Property TargetFileName As String

    <JsonPropertyName("patch_file_name")>
    Public Property PatchFileName As String
End Class
