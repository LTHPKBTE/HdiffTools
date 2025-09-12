Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes

Public Class 差分包合并
    Private ReadOnly 日志回调 As Action(Of String)
    Private ReadOnly 执行命令回调 As Action(Of String)
    Private ReadOnly 压缩器 As 压缩包处理

    Public Sub New(日志回调 As Action(Of String), 执行命令回调 As Action(Of String), 压缩器 As 压缩包处理)
        Me.日志回调 = 日志回调
        Me.执行命令回调 = 执行命令回调
        Me.压缩器 = 压缩器
    End Sub

    Public Class 合并参数
        Public Property 客户端路径 As String
        Public Property 差分包路径 As String
        Public Property 语音差分包路径 As String
        Public Property 差分包是否压缩包 As Boolean
        Public Property 语音差分包是否压缩包 As Boolean
        Public Property V2差分包 As Boolean
        Public Property V2语音差分包 As Boolean
        Public Property Ldiff差分包 As Boolean
        Public Property Ldiff语音差分包 As Boolean
        Public Property 需要合并语音包 As Boolean
        Public Property hpatchzExe As String
    End Class

    Public Sub 合并差分包(参数 As 合并参数)
        ' 解压差分包
        If 参数.差分包是否压缩包 Then
            Dim 压缩文件目录 As String = Path.GetDirectoryName(参数.差分包路径)
            Dim 压缩文件名 As String = Path.GetFileNameWithoutExtension(参数.差分包路径)
            Dim 压缩文件路径 As String = 参数.差分包路径
            参数.差分包路径 = Path.Combine(压缩文件目录, 压缩文件名)
            If Directory.Exists(参数.差分包路径) Then Directory.Delete(参数.差分包路径, True)
            日志回调("解压：" & 压缩文件路径 & "...")
            压缩器.解压压缩包(压缩文件路径, 参数.差分包路径)
        End If

        ' 解压语音差分包
        If 参数.需要合并语音包 And 参数.语音差分包是否压缩包 Then
            Dim 压缩文件目录 As String = Path.GetDirectoryName(参数.语音差分包路径)
            Dim 压缩文件名 As String = Path.GetFileNameWithoutExtension(参数.语音差分包路径)
            Dim 压缩文件路径 As String = 参数.语音差分包路径
            参数.语音差分包路径 = Path.Combine(压缩文件目录, 压缩文件名)
            If Directory.Exists(参数.语音差分包路径) Then Directory.Delete(参数.语音差分包路径, True)
            日志回调("解压：" & 压缩文件路径 & "...")
            压缩器.解压压缩包(压缩文件路径, 参数.语音差分包路径)
        End If

        Dim 差分包路径 As String
        Dim 语音差分包路径 As String

        ' Ldiff转换
        If 参数.Ldiff差分包 Then
            Dim 输出路径 As String = Path.Combine(参数.客户端路径, "hdiff")
            Ldiff转换(日志回调, 参数.差分包路径, 输出路径, 参数.差分包是否压缩包)
            差分包路径 = 输出路径
            参数.V2差分包 = True
        Else
            差分包路径 = 参数.差分包路径
        End If

        If 参数.需要合并语音包 And 参数.Ldiff语音差分包 Then
            Dim 输出路径 As String = Path.Combine(参数.客户端路径, "hdiff_audio")
            Ldiff转换(日志回调, 参数.语音差分包路径, 输出路径, 参数.语音差分包是否压缩包)
            语音差分包路径 = 输出路径
            参数.V2语音差分包 = True
        Else
            语音差分包路径 = 参数.语音差分包路径
        End If

        Dim deleteFiles As List(Of String)
        If File.Exists(Path.Combine(差分包路径, "deletefiles.txt")) Then
            deleteFiles = File.ReadLines(Path.Combine(差分包路径, "deletefiles.txt")).ToList()
        Else
            deleteFiles = New List(Of String)()
        End If

        Dim deleteFilesAudio As List(Of String)
        If 参数.需要合并语音包 Then
            If File.Exists(Path.Combine(语音差分包路径, "deletefiles.txt")) Then
                deleteFilesAudio = File.ReadLines(Path.Combine(语音差分包路径, "deletefiles.txt")).ToList()
            Else
                deleteFilesAudio = New List(Of String)()
            End If
        Else
            deleteFilesAudio = New List(Of String)()
        End If

        删除只读属性(参数.客户端路径)
        删除只读属性(差分包路径)
        If 参数.需要合并语音包 Then 删除只读属性(语音差分包路径)

        Dim temp目录 As String = Path.Combine(参数.客户端路径, "temp")
        Directory.CreateDirectory(temp目录)

        删除文件(参数.客户端路径, deleteFiles)
        应用补丁(参数.客户端路径, 差分包路径, temp目录, 参数.V2差分包, 参数.hpatchzExe)
        移动文件(差分包路径, 参数.客户端路径)
        Directory.Delete(差分包路径, True)

        If 参数.需要合并语音包 Then
            删除文件(参数.客户端路径, deleteFilesAudio)
            应用补丁(参数.客户端路径, 语音差分包路径, temp目录, 参数.V2语音差分包, 参数.hpatchzExe)
            移动文件(语音差分包路径, 参数.客户端路径)
            Directory.Delete(语音差分包路径, True)
        End If

        日志回调("合并完成!")
    End Sub

    Private Sub 删除只读属性(path As String)
        For Each file As String In Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            Dim fileInfo As New FileInfo(file)
            fileInfo.Attributes = FileAttributes.Normal
        Next
    End Sub

    Private Sub 应用补丁(客户端目录 As String, 差分包目录 As String, 临时目录 As String, IsV2 As Boolean, hpatchzExe As String)
        If Not IsV2 Then
            Dim hdiffFiles As List(Of String) = File.ReadLines(Path.Combine(差分包目录, "hdifffiles.txt")).ToList()
            For Each line As String In hdiffFiles
                Dim patchInfo As JsonObject = JsonSerializer.Deserialize(Of JsonObject)(line)
                Dim remoteName As String = patchInfo("remoteName").ToString()
                Dim baseName As String = Path.GetFileName(remoteName)

                日志回调($"合并：{baseName}...")

                Dim 源文件 As String = Path.Combine(客户端目录, remoteName)
                Dim hdiff文件 As String = Path.Combine(差分包目录, remoteName & ".hdiff")
                Dim 目标文件 As String = Path.Combine(临时目录, baseName)

                Dim cmd As String = $"""{hpatchzExe}"" ""{源文件}"" ""{hdiff文件}"" ""{目标文件}"""
                执行命令回调(cmd)

                If File.Exists(源文件) Then File.Delete(源文件)
                If File.Exists(hdiff文件) Then File.Delete(hdiff文件)
                File.Move(目标文件, 源文件)
            Next
        Else
            Dim hdiffFiles As HDiffMap = JsonSerializer.Deserialize(Of HDiffMap)(File.ReadAllText(Path.Combine(差分包目录, "hdiffmap.json")))
            For Each json As HDiffData In hdiffFiles.DiffMap
                Dim sourceFileName As String = json.SourceFileName
                Dim targetFileName As String = json.TargetFileName
                Dim patchFileName As String = json.PatchFileName

                日志回调($"合并：{targetFileName}...")

                Dim 源文件 As String = Path.Combine(客户端目录, sourceFileName)
                Dim hdiff文件 As String = Path.Combine(差分包目录, patchFileName)
                Dim 目标文件 As String = Path.Combine(客户端目录, targetFileName)
                Dim 临时文件 As String = Path.Combine(临时目录, Path.GetFileName(targetFileName))

                If Not File.Exists(hdiff文件) Then
                    日志回调("警告：找不到hdiff文件：" & hdiff文件)
                    Continue For
                End If

                Dim cmd As String
                If Not sourceFileName = "" Then
                    If Not File.Exists(源文件) Then
                        日志回调("警告：找不到源文件：" & 源文件)
                        If File.Exists(hdiff文件) Then File.Delete(hdiff文件)
                        Continue For
                    End If

                    cmd = $"""{hpatchzExe}"" ""{源文件}"" ""{hdiff文件}"" ""{临时文件}"""
                Else
                    cmd = $"""{hpatchzExe}"" -f """" ""{hdiff文件}"" ""{临时文件}"""
                End If
                执行命令回调(cmd)

                If File.Exists(源文件) And Not sourceFileName = "" Then File.Delete(源文件)
                If File.Exists(hdiff文件) Then File.Delete(hdiff文件)
                If File.Exists(目标文件) Then File.Delete(目标文件)
                File.Move(临时文件, 目标文件)
            Next
        End If
    End Sub

    Private Sub 删除文件(文件路径 As String, 文件列表 As List(Of String))
        For Each df As String In 文件列表
            Dim filePath As String = Path.Combine(文件路径, df.Trim())
            If File.Exists(filePath) Then
                File.Delete(filePath)
                日志回调($"删除：{filePath}")
            End If
        Next
    End Sub

    Private Sub 移动文件(源路径 As String, 目标路径 As String)
        For Each 源文件 As String In Directory.GetFiles(源路径, "*", SearchOption.AllDirectories)
            Dim 相对路径 As String = 源文件.Substring(源路径.Length + 1)
            Dim 目标文件 As String = Path.Combine(目标路径, 相对路径)
            Dim 目的文件夹 As String = Path.GetDirectoryName(目标文件)
            If Not Directory.Exists(目的文件夹) Then
                Directory.CreateDirectory(目的文件夹)
            End If
            日志回调($"移动：{源文件} -> {目标文件}")
            If File.Exists(目标文件) Then File.Delete(目标文件)
            File.Move(源文件, 目标文件)
        Next
    End Sub
End Class
