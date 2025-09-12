Imports System.IO
Imports System.Security.Cryptography
Imports System.Text.Json

Public Class 差分包制作
    Private ReadOnly 日志回调 As Action(Of String)
    Private ReadOnly 执行命令回调 As Action(Of String)
    Private ReadOnly 压缩器 As 压缩包处理

    Public Sub New(日志回调 As Action(Of String), 执行命令回调 As Action(Of String), 压缩器 As 压缩包处理)
        Me.日志回调 = 日志回调
        Me.执行命令回调 = 执行命令回调
        Me.压缩器 = 压缩器
    End Sub

    Public Sub 制作差分包(旧客户端路径 As String, 新客户端路径 As String, 差分包保存路径 As String, hdiffzExe As String)
        Dim 输出目录 As String = Path.GetDirectoryName(差分包保存路径)
        Dim 压缩包名称 As String = Path.GetFileName(差分包保存路径)
        Dim 临时目录 As String = Path.Combine(输出目录, Path.GetFileNameWithoutExtension(差分包保存路径))

        日志回调($"旧版本目录: {旧客户端路径}")
        日志回调($"新版本目录: {新客户端路径}")
        日志回调($"输出压缩包: {差分包保存路径}")

        日志回调($"创建临时目录: {临时目录}")
        If Directory.Exists(临时目录) Then
            Directory.Delete(临时目录, True)
        End If

        Directory.CreateDirectory(临时目录)

        Dim 旧文件集合 As HashSet(Of String) = 获取文件列表(旧客户端路径)
        Dim 新文件集合 As HashSet(Of String) = 获取文件列表(新客户端路径)

        生成删除文件列表(旧文件集合, 新文件集合, 临时目录)
        生成补丁文件(旧客户端路径, 新客户端路径, 临时目录, 旧文件集合, 新文件集合, hdiffzExe)
        添加新增文件(新客户端路径, 临时目录, 旧文件集合, 新文件集合)
        压缩器.创建压缩包(临时目录, 差分包保存路径)

        Directory.Delete(临时目录, True)
    End Sub

    Private Function 获取文件列表(目录路径 As String) As HashSet(Of String)
        Dim 文件列表 As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim 文件数量 As Integer = 0

        For Each 文件路径 In Directory.GetFiles(目录路径, "*", SearchOption.AllDirectories)
            Dim 相对路径 As String = 获取相对路径(目录路径, 文件路径).Replace("\", "/")
            文件列表.Add(相对路径)
            文件数量 += 1
        Next

        Return 文件列表
    End Function

    Private Function 获取相对路径(基础路径 As String, 完整路径 As String) As String
        If Not 基础路径.EndsWith(Path.DirectorySeparatorChar) Then
            基础路径 &= Path.DirectorySeparatorChar
        End If

        Dim 基础Uri As New Uri(基础路径)
        Dim 完整Uri As New Uri(完整路径)
        Dim 相对Uri As Uri = 基础Uri.MakeRelativeUri(完整Uri)
        Return Uri.UnescapeDataString(相对Uri.ToString()).Replace("/", Path.DirectorySeparatorChar)
    End Function

    Private Sub 生成删除文件列表(旧文件集合 As HashSet(Of String), 新文件集合 As HashSet(Of String), 输出目录 As String)
        Dim 已删除文件集合 As New HashSet(Of String)(旧文件集合)
        已删除文件集合.ExceptWith(新文件集合)

        Dim 删除文件路径 As String = Path.Combine(输出目录, "deletefiles.txt")
        If File.Exists(删除文件路径) Then File.Delete(删除文件路径)

        Using 写入器 As New StreamWriter(删除文件路径)
            For Each 文件路径 In 已删除文件集合
                写入器.WriteLine(文件路径)
                日志回调($"待删除文件: {文件路径}")
            Next
        End Using
    End Sub

    Private Sub 生成补丁文件(旧目录 As String, 新目录 As String, 输出目录 As String, 旧文件集合 As HashSet(Of String), 新文件集合 As HashSet(Of String), hdiffzExe As String)
        Dim 共有文件集合 As New HashSet(Of String)(旧文件集合)
        共有文件集合.IntersectWith(新文件集合)
        Dim 补丁条目列表 As New List(Of Dictionary(Of String, String))
        Dim 补丁数量 As Integer = 0
        Dim 跳过数量 As Integer = 0

        For Each 文件相对路径 In 共有文件集合
            Dim 文件名 As String = Path.GetFileName(文件相对路径)
            If 文件名 = "hdifffiles.txt" OrElse 文件名 = "deletefiles.txt" Then
                日志回调($"跳过差分包生成文件: {文件相对路径}")
                Continue For
            End If

            Dim 旧文件路径 As String = Path.Combine(旧目录, 文件相对路径)
            Dim 新文件路径 As String = Path.Combine(新目录, 文件相对路径)

            If 文件是否相同(旧文件路径, 新文件路径) Then
                日志回调($"跳过相同文件: {文件相对路径}")
                跳过数量 += 1
                Continue For
            End If

            Dim 补丁文件路径 As String = Path.Combine(输出目录, 文件相对路径 & ".hdiff")
            Directory.CreateDirectory(Path.GetDirectoryName(补丁文件路径))

            日志回调($"生成差分文件: {补丁文件路径}")
            Dim cmd As String = $"""{hdiffzExe}"" ""{旧文件路径}"" ""{新文件路径}"" ""{补丁文件路径}"""
            执行命令回调(cmd)

            补丁条目列表.Add(New Dictionary(Of String, String) From {{"remoteName", 文件相对路径}})
            补丁数量 += 1
        Next

        Dim 补丁列表路径 As String = Path.Combine(输出目录, "hdifffiles.txt")

        If File.Exists(补丁列表路径) Then File.Delete(补丁列表路径)
        Using 写入器 As New StreamWriter(补丁列表路径)
            For Each 条目 In 补丁条目列表
                写入器.WriteLine(JsonSerializer.Serialize(条目, New JsonSerializerOptions With {.WriteIndented = False}))
            Next
        End Using
    End Sub

    Private Function 文件是否相同(文件路径1 As String, 文件路径2 As String) As Boolean
        Dim 文件信息1 As New FileInfo(文件路径1)
        Dim 文件信息2 As New FileInfo(文件路径2)

        If 文件信息1.Length <> 文件信息2.Length Then Return False

        Using MD5计算器 As MD5 = MD5.Create()
            Dim md5文件1 As String
            Using 文件流 As FileStream = File.OpenRead(文件路径1)
                md5文件1 = BitConverter.ToString(MD5计算器.ComputeHash(文件流)).Replace("-", "").ToLowerInvariant()
            End Using

            MD5计算器.Initialize()

            Dim md5文件2 As String
            Using 文件流 As FileStream = File.OpenRead(文件路径2)
                md5文件2 = BitConverter.ToString(MD5计算器.ComputeHash(文件流)).Replace("-", "").ToLowerInvariant()
            End Using

            Return md5文件1 = md5文件2
        End Using
    End Function

    Private Sub 添加新增文件(新目录 As String, 输出目录 As String, 旧文件集合 As HashSet(Of String), 新文件集合 As HashSet(Of String))
        Dim 新增文件集合 As New HashSet(Of String)(新文件集合)
        新增文件集合.ExceptWith(旧文件集合)
        Dim 新增数量 As Integer = 0

        For Each 文件相对路径 In 新增文件集合
            Dim 文件名 As String = Path.GetFileName(文件相对路径)
            If 文件名 = "hdifffiles.txt" OrElse 文件名 = "deletefiles.txt" Then
                日志回调($"跳过差分包生成文件: {文件相对路径}")
                Continue For
            End If

            Dim 源文件路径 As String = Path.Combine(新目录, 文件相对路径)
            Dim 目标文件路径 As String = Path.Combine(输出目录, 文件相对路径)

            Directory.CreateDirectory(Path.GetDirectoryName(目标文件路径))
            File.Copy(源文件路径, 目标文件路径, True)

            日志回调($"新文件: {目标文件路径}")
            新增数量 += 1
        Next
    End Sub
End Class
