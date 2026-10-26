Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class GedEvent
    Public Property Type As String = ""
    Public Property DateText As String = ""
    Public Property Place As String = ""
    Public Property Description As String = ""
End Class

Public Class Individual
    Public Property Id As String = ""
    Public Property Name As String = ""
    Public Property Sex As String = "U"
    Public Property Events As New List(Of GedEvent)
    Public Property Famc As String = Nothing
    Public Property Fams As New List(Of String)
End Class

Public Class Family
    Public Property Id As String = ""
    Public Property Husb As String = Nothing
    Public Property Wife As String = Nothing
    Public Property Children As New List(Of String)
    Public Property Events As New List(Of GedEvent)
End Class

Public Class MainForm
    Inherits Form

    Private ReadOnly individuals As New Dictionary(Of String, Individual)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly families As New Dictionary(Of String, Family)(StringComparer.OrdinalIgnoreCase)
    Private selectedIndiId As String = Nothing
    Private isRefreshingPeopleList As Boolean = False
    Private lastLoadedGedcom As String = ""

    Private ReadOnly peopleList As New ListBox()
    Private ReadOnly searchBox As New TextBox()
    Private ReadOnly details As New TabControl()
    Private ReadOnly statusLabel As New Label()

    Public Sub New()
        Text = "🌳Editor de Arquivos GEDCOM - VB.NET"
        Width = 1250
        Height = 800
        MinimumSize = New Size(900, 600)
        StartPosition = FormStartPosition.CenterScreen
        BuildInterface()
        LoadSample()
    End Sub

    Private Sub BuildInterface()
        Dim header As New FlowLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 48, .Padding = New Padding(8),
            .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False,
            .AutoScroll = True
        }
        Dim loadButton = MakeButton("📁 Carregar GEDCOM")
        AddHandler loadButton.Click, Sub() LoadGedcomFile()
        Dim saveButton = MakeButton("💾 Salvar Arquivo")
        AddHandler saveButton.Click, Sub() SaveGedcom(False, False)
        Dim ancestorsButton = MakeButton("👴 Salvar Ascendentes de @I1@")
        AddHandler ancestorsButton.Click, Sub() SaveGedcom(True, False)
        Dim extendedButton = MakeButton("👨‍👩‍👧 Exportar ascendentes, filhos e famílias")
        AddHandler extendedButton.Click, Sub() SaveGedcom(False, True)
        Dim addButton = MakeButton("👤 Nova Pessoa")
        AddHandler addButton.Click, Sub() AddIndividual()
        Dim validateButton = MakeButton("📄 Validar GEDCOM")
        AddHandler validateButton.Click, AddressOf ValidateLoadedGedcom
        Dim treeButton = MakeButton("🌳 Árvore genealógica")
        AddHandler treeButton.Click, AddressOf ShowGenealogyTree
        header.Controls.AddRange({loadButton, saveButton, ancestorsButton, extendedButton, addButton, validateButton, treeButton})

        Dim split As New SplitContainer With {.Dock = DockStyle.Fill, .SplitterDistance = 32}
        Dim left As New Panel With {.Dock = DockStyle.Fill}
        searchBox.Dock = DockStyle.Top
        searchBox.PlaceholderText = "🔍 Buscar por nome ou ID..."
        AddHandler searchBox.TextChanged, Sub() RefreshPeopleList()
        peopleList.Dock = DockStyle.Fill
        AddHandler peopleList.SelectedIndexChanged, AddressOf PeopleSelectionChanged
        left.Controls.Add(peopleList)
        left.Controls.Add(searchBox)
        split.Panel1.Controls.Add(left)

        details.Dock = DockStyle.Fill
        split.Panel2.Controls.Add(details)
        statusLabel.Dock = DockStyle.Bottom
        statusLabel.Height = 24
        statusLabel.Padding = New Padding(8, 4, 0, 0)
        statusLabel.Text = "Pronto."
        Controls.Add(split)
        Controls.Add(statusLabel)
        Controls.Add(header)
    End Sub

    Private Function MakeButton(caption As String) As Button
        Return New Button With {.Text = caption, .AutoSize = True, .Height = 30, .Margin = New Padding(3)}
    End Function

    Private Sub LoadSample()
        Dim sample As String = String.Join(vbCrLf, {
            "0 HEAD", "1 SOUR DEMO", "1 GEDC", "2 VERS 5.5.1",
            "0 @I1@ INDI", "1 NAME João /Silva/", "1 SEX M", "1 BIRT", "2 DATE 10 MAY 1960", "2 PLAC São Paulo, SP", "1 FAMS @F1@",
            "0 @I2@ INDI", "1 NAME Maria /Santos/", "1 SEX F", "1 BIRT", "2 DATE 15 AUG 1962", "2 PLAC Rio de Janeiro, RJ", "1 FAMS @F1@",
            "0 @I3@ INDI", "1 NAME Lucas /Silva/", "1 SEX M", "1 BIRT", "2 DATE 02 APR 1990", "2 PLAC Campinas, SP", "1 FAMC @F1@",
            "0 @F1@ FAM", "1 HUSB @I1@", "1 WIFE @I2@", "1 CHIL @I3@", "1 MARR", "2 DATE 12 DEC 1985", "2 PLAC São Paulo, SP", "0 TRLR"
        })
        ParseGedcom(sample)
    End Sub

    Private Sub LoadGedcomFile()
        Using dlg As New OpenFileDialog With {
            .Filter = "Arquivos GEDCOM (*.ged;*.gedcom)|*.ged;*.gedcom|Todos os arquivos (*.*)|*.*",
            .Title = "📥 Carregar arquivo GEDCOM"
        }
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                ParseGedcom(File.ReadAllText(dlg.FileName, Encoding.UTF8))
                statusLabel.Text = "📄 Carregado: " & dlg.FileName
            Catch ex As Exception
                MessageBox.Show("⚠️ Não foi possível carregar o arquivo: " & ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub ParseGedcom(content As String)
        lastLoadedGedcom = content
        individuals.Clear()
        families.Clear()
        Dim currentRecordType As String = Nothing
        Dim currentIndividual As Individual = Nothing
        Dim currentFamily As Family = Nothing
        Dim currentEvent As GedEvent = Nothing
        Dim rx As New Regex("^(\d+)\s+(@[^@]+@|[A-Za-z0-9_]+)?\s*(.*)$")

        For Each rawLine In content.Split({vbCrLf, vbLf}, StringSplitOptions.None)
            Dim line = rawLine.Trim()
            If line.Length = 0 Then Continue For
            Dim m = rx.Match(line)
            If Not m.Success Then Continue For
            Dim level = Integer.Parse(m.Groups(1).Value)
            Dim tagOrId = m.Groups(2).Value
            Dim rest = m.Groups(3).Value.Trim()

            If level = 0 Then
                currentEvent = Nothing
                currentIndividual = Nothing
                currentFamily = Nothing
                If rest = "INDI" Then
                    currentRecordType = "INDI"
                    currentIndividual = New Individual With {.Id = tagOrId}
                    individuals(tagOrId) = currentIndividual
                ElseIf rest = "FAM" Then
                    currentRecordType = "FAM"
                    currentFamily = New Family With {.Id = tagOrId}
                    families(tagOrId) = currentFamily
                Else
                    currentRecordType = Nothing
                End If
                Continue For
            End If

            If currentRecordType = "INDI" AndAlso currentIndividual IsNot Nothing Then
                If level = 1 Then
                    currentEvent = Nothing
                    Select Case tagOrId
                        Case "NAME"
                            currentIndividual.Name = rest.Replace("/", "").Trim()
                        Case "SEX"
                            currentIndividual.Sex = rest.ToUpperInvariant()
                        Case "FAMS"
                            currentIndividual.Fams.Add(rest)
                        Case "FAMC"
                            currentIndividual.Famc = rest
                        Case "BIRT", "DEAT", "BAPM", "CHR", "BURI", "CREM", "OCCU", "RESI", "EVEN", "EDUC", "IMMI", "EMIG", "NATU", "RETI"
                            currentEvent = New GedEvent With {.Type = tagOrId, .Description = rest}
                            currentIndividual.Events.Add(currentEvent)
                    End Select
                ElseIf level = 2 AndAlso currentEvent IsNot Nothing Then
                    If tagOrId = "DATE" Then currentEvent.DateText = rest
                    If tagOrId = "PLAC" Then currentEvent.Place = rest
                End If
            ElseIf currentRecordType = "FAM" AndAlso currentFamily IsNot Nothing Then
                If level = 1 Then
                    currentEvent = Nothing
                    Select Case tagOrId
                        Case "HUSB" : currentFamily.Husb = rest
                        Case "WIFE" : currentFamily.Wife = rest
                        Case "CHIL" : currentFamily.Children.Add(rest)
                        Case "MARR", "DIV", "ENGA", "ANUL", "MARB", "MARC", "MARL", "RESI", "EVEN"
                            currentEvent = New GedEvent With {.Type = tagOrId, .Description = rest}
                            currentFamily.Events.Add(currentEvent)
                    End Select
                ElseIf level = 2 AndAlso currentEvent IsNot Nothing Then
                    If tagOrId = "DATE" Then currentEvent.DateText = rest
                    If tagOrId = "PLAC" Then currentEvent.Place = rest
                End If
            End If
        Next

        CleanReferences()
        RefreshPeopleList()
        If individuals.ContainsKey("@I1@") Then
            SelectPerson("@I1@")
        ElseIf individuals.Count > 0 Then
            SelectPerson(individuals.Keys.First())
        Else
            details.TabPages.Clear()
        End If
    End Sub

    Private Sub ValidateLoadedGedcom(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(lastLoadedGedcom) Then
            MessageBox.Show("🛑 Carregue um arquivo GEDCOM antes de executar a validação.", "Validação GEDCOM", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim issues As New List(Of String)()
        Dim source = lastLoadedGedcom
        Dim lines = source.Split({vbCrLf, vbLf}, StringSplitOptions.None)
        Dim lineRx As New Regex("^(\d+)\s+(@[^@]+@|[A-Za-z0-9_]+)?\s*(.*)$")
        Dim recordIds As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim individualFamc As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim individualFams As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim familyHusb As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim familyWife As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim familyChildren As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim recordType As String = Nothing
        Dim recordId As String = Nothing
        Dim previousLevel As Integer = 0
        Dim firstNonEmpty As Boolean = True
        Dim headCount As Integer = 0
        Dim trailerCount As Integer = 0
        Dim trailerLine As Integer = -1

        For i As Integer = 0 To lines.Length - 1
            Dim line = lines(i).Trim()
            If line.Length = 0 Then Continue For
            Dim lineNo = i + 1
            Dim m = lineRx.Match(line)
            If Not m.Success OrElse String.IsNullOrWhiteSpace(m.Groups(1).Value) Then
                issues.Add($"⚠️ Linha {lineNo}: estrutura de linha inválida: {line}")
                Continue For
            End If

            Dim level As Integer
            If Not Integer.TryParse(m.Groups(1).Value, level) Then
                issues.Add($"⚠️Linha {lineNo}: nível inválido.")
                Continue For
            End If
            Dim tagOrId = m.Groups(2).Value
            Dim rest = m.Groups(3).Value.Trim()

            If firstNonEmpty Then
                firstNonEmpty = False
                If level <> 0 OrElse tagOrId <> "HEAD" Then issues.Add($"⚠️ {lineNo}: o primeiro registro deve ser '0 HEAD'.")
            End If
            If level > previousLevel + 1 Then issues.Add($"Linha {lineNo}: salto de nível inválido ({previousLevel} para {level}).")
            If level = 0 AndAlso previousLevel <> 0 AndAlso recordType IsNot Nothing Then
                ' O nível zero inicia um novo registro principal.
            End If
            If trailerCount > 0 Then issues.Add($"⚠️ Linha {lineNo}: há conteúdo após o registro TRLR.")

            If level = 0 Then
                recordId = If(tagOrId.StartsWith("@", StringComparison.Ordinal), tagOrId, Nothing)
                recordType = If(recordId IsNot Nothing, rest.Split({" "c}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), tagOrId)
                If recordType = "HEAD" Then headCount += 1
                If recordType = "TRLR" Then
                    trailerCount += 1
                    trailerLine = lineNo
                End If
                If recordId IsNot Nothing Then
                    Dim actualType = rest.Split({" "c}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                    If String.IsNullOrWhiteSpace(actualType) Then
                        issues.Add($"⚠️ Linha {lineNo}: registro {recordId} sem tipo.")
                    ElseIf recordIds.ContainsKey(recordId) Then
                        issues.Add($"⚠️ Linha {lineNo}: identificador duplicado {recordId} (já usado como {recordIds(recordId)}).")
                    Else
                        recordIds(recordId) = actualType
                    End If
                    If actualType = "INDI" Then
                        individualFamc(recordId) = New List(Of String)()
                        individualFams(recordId) = New List(Of String)()
                    ElseIf actualType = "FAM" Then
                        familyHusb(recordId) = New List(Of String)()
                        familyWife(recordId) = New List(Of String)()
                        familyChildren(recordId) = New List(Of String)()
                    End If
                    recordType = actualType
                End If
            ElseIf level = 1 AndAlso recordId IsNot Nothing Then
                Select Case recordType
                    Case "INDI"
                        If tagOrId = "FAMC" AndAlso individualFamc.ContainsKey(recordId) Then individualFamc(recordId).Add(rest)
                        If tagOrId = "FAMS" AndAlso individualFams.ContainsKey(recordId) Then individualFams(recordId).Add(rest)
                    Case "FAM"
                        If tagOrId = "HUSB" AndAlso familyHusb.ContainsKey(recordId) Then familyHusb(recordId).Add(rest)
                        If tagOrId = "WIFE" AndAlso familyWife.ContainsKey(recordId) Then familyWife(recordId).Add(rest)
                        If tagOrId = "CHIL" AndAlso familyChildren.ContainsKey(recordId) Then familyChildren(recordId).Add(rest)
                End Select
            End If
            previousLevel = level
        Next

        If headCount = 0 Then issues.Add("⚠️Estrutura: não foi encontrado o registro obrigatório HEAD.")
        If headCount > 1 Then issues.Add($"⚠️ Estrutura: foram encontrados {headCount} registros HEAD; deveria existir apenas um.")
        If trailerCount = 0 Then
            issues.Add("⚠️ Estrutura: não foi encontrado o registro obrigatório TRLR.")
        ElseIf trailerCount > 1 Then
            issues.Add($"⚠️ Estrutura: foram encontrados {trailerCount} registros TRLR; deveria existir apenas um.")
        ElseIf trailerLine > 0 Then
            Dim lastLine = lines.Length
            While lastLine > 0 AndAlso String.IsNullOrWhiteSpace(lines(lastLine - 1))
                lastLine -= 1
            End While
            If trailerLine <> lastLine Then issues.Add("⚠️ Estrutura: TRLR não é o último registro do arquivo.")
        End If

        Dim allIndividuals = New HashSet(Of String)(individualFamc.Keys, StringComparer.OrdinalIgnoreCase)
        Dim allFamilies = New HashSet(Of String)(familyHusb.Keys, StringComparer.OrdinalIgnoreCase)
        For Each kv In individualFamc
            For Each famId In kv.Value
                If Not allFamilies.Contains(famId) Then
                    issues.Add($"⚠️ Referência inexistente: indivíduo {kv.Key} aponta FAMC para família {famId}.")
                ElseIf kv.Value.Count > 1 Then
                    issues.Add($"⚠️ Vínculo suspeito: indivíduo {kv.Key} possui mais de uma referência FAMC.")
                End If
            Next
        Next
        For Each kv In individualFams
            For Each famId In kv.Value
                If Not allFamilies.Contains(famId) Then
                    issues.Add($"⚠️ Referência inexistente: indivíduo {kv.Key} aponta FAMS para família {famId}.")
                End If
            Next
            If kv.Value.Distinct(StringComparer.OrdinalIgnoreCase).Count() <> kv.Value.Count Then issues.Add($"⚠️ Vínculo duplicado: indivíduo {kv.Key} repete uma referência FAMS.")
        Next
        For Each kv In familyHusb
            For Each personId In kv.Value
                If Not allIndividuals.Contains(personId) Then
                    issues.Add($"⚠️ Referência inexistente: família {kv.Key} aponta HUSB para indivíduo {personId}.")
                ElseIf Not individualFams(personId).Contains(kv.Key, StringComparer.OrdinalIgnoreCase) Then
                    issues.Add($"⚠️ Vínculo inconsistente: família {kv.Key} lista HUSB {personId}, mas o indivíduo não lista FAMS para essa família.")
                End If
            Next
            If kv.Value.Count > 1 Then issues.Add($"⚠️ Vínculo inconsistente: família {kv.Key} possui mais de um HUSB.")
        Next
        For Each kv In familyWife
            For Each personId In kv.Value
                If Not allIndividuals.Contains(personId) Then
                    issues.Add($"⚠️ Referência inexistente: família {kv.Key} aponta WIFE para indivíduo {personId}.")
                ElseIf Not individualFams(personId).Contains(kv.Key, StringComparer.OrdinalIgnoreCase) Then
                    issues.Add($"⚠️ Vínculo inconsistente: família {kv.Key} lista WIFE {personId}, mas o indivíduo não lista FAMS para essa família.")
                End If
            Next
            If kv.Value.Count > 1 Then issues.Add($"⚠️ Vínculo inconsistente: família {kv.Key} possui mais de um WIFE.")
        Next
        For Each kv In familyChildren
            For Each childId In kv.Value
                If Not allIndividuals.Contains(childId) Then
                    issues.Add($"⚠️ Referência inexistente: família {kv.Key} aponta CHIL para indivíduo {childId}.")
                ElseIf Not individualFamc(childId).Contains(kv.Key, StringComparer.OrdinalIgnoreCase) Then
                    issues.Add($"⚠️ Vínculo inconsistente: família {kv.Key} lista CHIL {childId}, mas o indivíduo não lista FAMC para essa família.")
                End If
            Next
            If kv.Value.Distinct(StringComparer.OrdinalIgnoreCase).Count() <> kv.Value.Count Then issues.Add($"⚠️ Vínculo duplicado: família {kv.Key} repete um filho em CHIL.")
        Next
        For Each kv In individualFams
            For Each famId In kv.Value
                If allFamilies.Contains(famId) Then
                    Dim spouseListed = (familyHusb(famId).Contains(kv.Key, StringComparer.OrdinalIgnoreCase) OrElse familyWife(famId).Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                    If Not spouseListed Then issues.Add($"⚠️ Vínculo inconsistente: indivíduo {kv.Key} lista FAMS {famId}, mas a família não o lista como HUSB/WIFE.")
                End If
            Next
        Next
        For Each kv In individualFamc
            For Each famId In kv.Value
                If allFamilies.Contains(famId) AndAlso Not familyChildren(famId).Contains(kv.Key, StringComparer.OrdinalIgnoreCase) Then
                    issues.Add($"⚠️ Vínculo inconsistente: indivíduo {kv.Key} lista FAMC {famId}, mas a família não o lista em CHIL.")
                End If
            Next
        Next

        ' Verifica também o estado atual em memória, incluindo alterações feitas no editor.
        For Each ind In individuals.Values
            If Not String.IsNullOrWhiteSpace(ind.Famc) Then
                If Not families.ContainsKey(ind.Famc) Then
                    issues.Add($"⚠️ Estado atual: indivíduo {ind.Id} aponta para a família parental inexistente {ind.Famc}.")
                ElseIf Not families(ind.Famc).Children.Contains(ind.Id, StringComparer.OrdinalIgnoreCase) Then
                    issues.Add($"⚠️ Estado atual: indivíduo {ind.Id} aponta FAMC para {ind.Famc}, mas não consta como filho dessa família.")
                End If
            End If
            For Each famId In ind.Fams
                If Not families.ContainsKey(famId) Then
                    issues.Add($"⚠️ Estado atual: indivíduo {ind.Id} aponta FAMS para a família inexistente {famId}.")
                ElseIf families(famId).Husb <> ind.Id AndAlso families(famId).Wife <> ind.Id Then
                    issues.Add($"⚠️ Estado atual: indivíduo {ind.Id} aponta FAMS para {famId}, mas não consta como cônjuge nessa família.")
                End If
            Next
        Next
        For Each fam In families.Values
            If Not String.IsNullOrWhiteSpace(fam.Husb) Then
                If Not individuals.ContainsKey(fam.Husb) Then
                    issues.Add($"⚠️ Estado atual: família {fam.Id} aponta HUSB para indivíduo inexistente {fam.Husb}.")
                ElseIf Not individuals(fam.Husb).Fams.Contains(fam.Id, StringComparer.OrdinalIgnoreCase) Then
                    issues.Add($"⚠️ Estado atual: família {fam.Id} lista HUSB {fam.Husb}, mas o indivíduo não possui FAMS correspondente.")
                End If
            End If
            If Not String.IsNullOrWhiteSpace(fam.Wife) Then
                If Not individuals.ContainsKey(fam.Wife) Then
                    issues.Add($"⚠️ Estado atual: família {fam.Id} aponta WIFE para indivíduo inexistente {fam.Wife}.")
                ElseIf Not individuals(fam.Wife).Fams.Contains(fam.Id, StringComparer.OrdinalIgnoreCase) Then
                    issues.Add($"⚠️ Estado atual: família {fam.Id} lista WIFE {fam.Wife}, mas o indivíduo não possui FAMS correspondente.")
                End If
            End If
            For Each childId In fam.Children
                If Not individuals.ContainsKey(childId) Then
                    issues.Add($"⚠️ Estado atual: família {fam.Id} aponta CHIL para indivíduo inexistente {childId}.")
                ElseIf individuals(childId).Famc <> fam.Id Then
                    issues.Add($"⚠️ Estado atual: família {fam.Id} lista CHIL {childId}, mas o indivíduo não aponta FAMC para essa família.")
                End If
            Next
        Next

        Dim distinctIssues = issues.Distinct().ToList()
        If distinctIssues.Count = 0 Then
            MessageBox.Show("Nenhum problema encontrado nas verificações realizadas.", "Validação GEDCOM", MessageBoxButtons.OK, MessageBoxIcon.Information)
            statusLabel.Text = "Validação GEDCOM: nenhum problema encontrado."
            Return
        End If

        Dim report As New StringBuilder()
        report.AppendLine($"Foram encontrados {distinctIssues.Count} problema(s):")
        report.AppendLine()
        For Each issue In distinctIssues
            report.AppendLine("• " & issue)
        Next
        Using dlg As New Form With {.Text = "📄 Relatório de validação GEDCOM", .Width = 900, .Height = 600, .StartPosition = FormStartPosition.CenterParent, .MinimizeBox = False, .MaximizeBox = True}
            Dim output As New TextBox With {.Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Both, .WordWrap = False, .Dock = DockStyle.Fill, .Font = New Font("Consolas", 10), .Text = report.ToString()}
            dlg.Controls.Add(output)
            dlg.ShowDialog(Me)
        End Using
        statusLabel.Text = $"Validação GEDCOM: {distinctIssues.Count} problema(s) encontrado(s)."
    End Sub

    Private Sub CleanReferences()
        For Each ind In individuals.Values
            If ind.Famc IsNot Nothing AndAlso Not families.ContainsKey(ind.Famc) Then ind.Famc = Nothing
            ind.Fams = ind.Fams.Where(Function(id) families.ContainsKey(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        Next
        For Each fam In families.Values
            If fam.Husb IsNot Nothing AndAlso Not individuals.ContainsKey(fam.Husb) Then fam.Husb = Nothing
            If fam.Wife IsNot Nothing AndAlso Not individuals.ContainsKey(fam.Wife) Then fam.Wife = Nothing
            fam.Children = fam.Children.Where(Function(id) individuals.ContainsKey(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        Next
        For Each famId In families.Keys.ToList()
            Dim fam = families(famId)
            If fam.Husb Is Nothing AndAlso fam.Wife Is Nothing AndAlso fam.Children.Count = 0 Then
                families.Remove(famId)
                For Each ind In individuals.Values
                    If ind.Famc = famId Then ind.Famc = Nothing
                    ind.Fams.RemoveAll(Function(x) String.Equals(x, famId, StringComparison.OrdinalIgnoreCase))
                Next
            End If
        Next
    End Sub

    Private Sub RefreshPeopleList()
        If isRefreshingPeopleList Then Return
        isRefreshingPeopleList = True
        Dim keepId = selectedIndiId
        peopleList.BeginUpdate()
        peopleList.Items.Clear()
        Dim term = searchBox.Text.Trim()
        For Each ind In individuals.Values.OrderBy(Function(x) x.Name)
            If term.Length > 0 AndAlso Not ind.Name.Contains(term, StringComparison.OrdinalIgnoreCase) AndAlso Not ind.Id.Contains(term, StringComparison.OrdinalIgnoreCase) Then Continue For
            peopleList.Items.Add(New PersonListItem(ind.Id, $"{ind.Name} ({ind.Id})"))
        Next
        peopleList.EndUpdate()
        statusLabel.Text = $"{peopleList.Items.Count} indivíduo(s)"
        For i = 0 To peopleList.Items.Count - 1
            If DirectCast(peopleList.Items(i), PersonListItem).Id = keepId Then
                peopleList.SelectedIndex = i
                Exit For
            End If
        Next
        isRefreshingPeopleList = False
    End Sub

    Private Class PersonListItem
        Public Property Id As String
        Private ReadOnly Caption As String
        Public Sub New(id As String, caption As String)
            Me.Id = id : Me.Caption = caption
        End Sub
        Public Overrides Function ToString() As String
            Return Caption
        End Function
    End Class

    Private Sub PeopleSelectionChanged(sender As Object, e As EventArgs)
        If isRefreshingPeopleList Then Return
        If TypeOf peopleList.SelectedItem Is PersonListItem Then
            SelectPerson(DirectCast(peopleList.SelectedItem, PersonListItem).Id)
        End If
    End Sub

    Private Sub SelectPerson(id As String)
        If Not individuals.ContainsKey(id) Then Return
        selectedIndiId = id
        RefreshPeopleList()
        RenderDetails()
    End Sub

    Private Sub RenderDetails()
        details.TabPages.Clear()
        If selectedIndiId Is Nothing OrElse Not individuals.ContainsKey(selectedIndiId) Then Return
        Dim ind = individuals(selectedIndiId)

        Dim personalTab As New TabPage("👤 Dados pessoais")
        Dim personalPanel As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(12)}
        Dim personalGrid As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .Padding = New Padding(4)}
        personalGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150))
        personalGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        AddField(personalGrid, "ID", New Label With {.Text = ind.Id, .AutoSize = True})
        Dim nameBox As New TextBox With {.Text = ind.Name, .Dock = DockStyle.Fill}
        AddField(personalGrid, "Nome completo", nameBox)
        AddHandler nameBox.TextChanged, Sub() ind.Name = nameBox.Text
        Dim sexBox As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Left, .Width = 180}
        sexBox.Items.AddRange({"M", "F", "U"})
        sexBox.SelectedItem = If({"M", "F", "U"}.Contains(ind.Sex), ind.Sex, "U")
        AddField(personalGrid, "Gênero (M/F/U)", sexBox)
        AddHandler sexBox.SelectedIndexChanged, Sub() ind.Sex = CStr(sexBox.SelectedItem)
        Dim deleteButton = MakeButton("Excluir pessoa")
        AddHandler deleteButton.Click, Sub() DeleteIndividual(ind.Id)
        AddField(personalGrid, "Ações", deleteButton)
        personalPanel.Controls.Add(personalGrid)
        personalTab.Controls.Add(personalPanel)
        details.TabPages.Add(personalTab)

        Dim eventsTab As New TabPage("📅 Eventos")
        Dim eventsPanel As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
        Dim eventsGrid As New DataGridView With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        eventsGrid.Columns.Add("Type", "Tipo")
        eventsGrid.Columns.Add("DateText", "Data")
        eventsGrid.Columns.Add("Place", "Local")
        eventsGrid.Columns.Add("Description", "Descrição")
        For Each ev In ind.Events
            eventsGrid.Rows.Add(ev.Type, ev.DateText, ev.Place, ev.Description)
        Next
        Dim eventsButtons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 42}
        Dim addEventButton = MakeButton("📅 Adicionar evento")
        AddHandler addEventButton.Click, Sub()
                                             Dim typ = InputBox("Tipo do evento GEDCOM (ex.: BIRT, DEAT, BAPM):", "Adicionar evento", "DEAT").Trim().ToUpperInvariant()
                                             If typ = "" Then Return
                                             ind.Events.Add(New GedEvent With {.Type = typ})
                                             RenderDetails()
                                         End Sub
        Dim removeEventButton = MakeButton("Excluir evento selecionado")
        AddHandler removeEventButton.Click, Sub()
                                                If eventsGrid.CurrentRow Is Nothing Then Return
                                                Dim idx = eventsGrid.CurrentRow.Index
                                                If idx >= 0 AndAlso idx < ind.Events.Count Then ind.Events.RemoveAt(idx)
                                                RenderDetails()
                                            End Sub
        Dim applyEventsButton = MakeButton("Aplicar alterações")
        AddHandler applyEventsButton.Click, Sub()
                                                For i = 0 To Math.Min(eventsGrid.Rows.Count, ind.Events.Count) - 1
                                                    Dim row = eventsGrid.Rows(i)
                                                    ind.Events(i).Type = CellText(row, 0).Trim().ToUpperInvariant()
                                                    ind.Events(i).DateText = CellText(row, 1)
                                                    ind.Events(i).Place = CellText(row, 2)
                                                    ind.Events(i).Description = CellText(row, 3)
                                                Next
                                                statusLabel.Text = "Eventos atualizados."
                                            End Sub
        eventsButtons.Controls.AddRange({addEventButton, removeEventButton, applyEventsButton})
        eventsPanel.Controls.Add(eventsGrid)
        eventsPanel.Controls.Add(eventsButtons)
        eventsTab.Controls.Add(eventsPanel)
        details.TabPages.Add(eventsTab)

        Dim relationsTab As New TabPage("👪 Família")
        relationsTab.Controls.Add(BuildRelationsPanel(ind))
        details.TabPages.Add(relationsTab)
    End Sub

    Private Function BuildRelationsPanel(ind As Individual) As Control
        Dim panel As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(12)}
        Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False}
        Dim parentsLabel As New Label With {.Text = "Pais", .Font = New Font(Font, FontStyle.Bold), .AutoSize = True}
        flow.Controls.Add(parentsLabel)

        Dim father As String = Nothing, mother As String = Nothing
        If ind.Famc IsNot Nothing AndAlso families.ContainsKey(ind.Famc) Then
            father = families(ind.Famc).Husb
            mother = families(ind.Famc).Wife
        End If
        flow.Controls.Add(New Label With {.Text = "Pai: " & PersonName(father), .AutoSize = True})
        flow.Controls.Add(New Label With {.Text = "Mãe: " & PersonName(mother), .AutoSize = True})
        Dim parentButtons As New FlowLayoutPanel With {.AutoSize = True}
        Dim addParents = MakeButton("Criar vínculo parental")
        AddHandler addParents.Click, Sub() AddParentFamily(ind.Id)
        parentButtons.Controls.Add(addParents)
        Dim setFather = MakeButton("Definir pai")
        AddHandler setFather.Click, Sub() SetParentInteractive(ind.Id, "HUSB")
        Dim setMother = MakeButton("Definir mãe")
        AddHandler setMother.Click, Sub() SetParentInteractive(ind.Id, "WIFE")
        Dim removeFather = MakeButton("Remover pai")
        AddHandler removeFather.Click, Sub() UnlinkParent(ind.Id, "HUSB")
        Dim removeMother = MakeButton("Remover mãe")
        AddHandler removeMother.Click, Sub() UnlinkParent(ind.Id, "WIFE")
        parentButtons.Controls.AddRange({setFather, setMother, removeFather, removeMother})
        flow.Controls.Add(parentButtons)

        Dim siblings As New List(Of String)
        If ind.Famc IsNot Nothing AndAlso families.ContainsKey(ind.Famc) Then
            siblings = families(ind.Famc).Children.Where(Function(x) x <> ind.Id).ToList()
        End If
        flow.Controls.Add(New Label With {.Text = "Irmãos: " & If(siblings.Count = 0, "nenhum registrado", String.Join(", ", siblings.Select(Function(x) PersonName(x)))), .AutoSize = True, .MaximumSize = New Size(850, 0)})

        flow.Controls.Add(New Label With {.Text = "Cônjuges, casamentos e filhos", .Font = New Font(Font, FontStyle.Bold), .AutoSize = True, .Padding = New Padding(0, 14, 0, 0)})
        Dim spouseButtons As New FlowLayoutPanel With {.AutoSize = True}
        Dim addSpouse = MakeButton("Adicionar cônjuge")
        AddHandler addSpouse.Click, Sub() AddSpouseInteractive(ind.Id)
        spouseButtons.Controls.Add(addSpouse)
        flow.Controls.Add(spouseButtons)

        For Each famId In ind.Fams.ToList()
            If Not families.ContainsKey(famId) Then Continue For
            Dim fam = families(famId)
            Dim spouseId = If(fam.Husb = ind.Id, fam.Wife, fam.Husb)
            Dim group As New GroupBox With {.Text = "Família " & fam.Id & " — Cônjuge: " & PersonName(spouseId), .Width = 900, .Height = 205, .Padding = New Padding(8)}
            Dim groupFlow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True}
            Dim eventGrid As New DataGridView With {.Width = 850, .Height = 75, .AllowUserToAddRows = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
            eventGrid.Columns.Add("Type", "Evento")
            eventGrid.Columns.Add("DateText", "Data")
            eventGrid.Columns.Add("Place", "Local")
            For Each ev In fam.Events
                eventGrid.Rows.Add(ev.Type, ev.DateText, ev.Place)
            Next
            Dim familyActions As New FlowLayoutPanel With {.AutoSize = True}
            Dim applyFam = MakeButton("Aplicar eventos familiares")
            AddHandler applyFam.Click, Sub()
                                           For i = 0 To Math.Min(eventGrid.Rows.Count, fam.Events.Count) - 1
                                               fam.Events(i).Type = CellText(eventGrid.Rows(i), 0).Trim().ToUpperInvariant()
                                               fam.Events(i).DateText = CellText(eventGrid.Rows(i), 1)
                                               fam.Events(i).Place = CellText(eventGrid.Rows(i), 2)
                                           Next
                                           statusLabel.Text = "Eventos familiares atualizados."
                                       End Sub
            Dim addFamEvent = MakeButton("Adicionar evento familiar")
            AddHandler addFamEvent.Click, Sub()
                                              Dim typ = InputBox("Tipo de evento (MARR, DIV, ENGA, ANUL):", "Evento familiar", "MARR").Trim().ToUpperInvariant()
                                              If typ = "" Then Return
                                              fam.Events.Add(New GedEvent With {.Type = typ})
                                              RenderDetails()
                                          End Sub
            Dim addChild = MakeButton("Adicionar filho")
            AddHandler addChild.Click, Sub() AddChildInteractive(fam.Id)
            Dim unlinkCouple = MakeButton("Desvincular casal")
            AddHandler unlinkCouple.Click, Sub() UnlinkSpouse(fam.Id)
            familyActions.Controls.AddRange({applyFam, addFamEvent, addChild, unlinkCouple})
            groupFlow.Controls.Add(eventGrid)
            groupFlow.Controls.Add(familyActions)
            Dim childrenLabel As New Label With {.AutoSize = True, .MaximumSize = New Size(850, 0),
                .Text = "Filhos: " & If(fam.Children.Count = 0, "nenhum", String.Join(", ", fam.Children.Select(Function(x) PersonName(x))))}
            groupFlow.Controls.Add(childrenLabel)
            group.Controls.Add(groupFlow)
            flow.Controls.Add(group)
        Next
        panel.Controls.Add(flow)
        Return panel
    End Function


    Private Sub ShowGenealogyTree(sender As Object, e As EventArgs)
        If selectedIndiId Is Nothing OrElse Not individuals.ContainsKey(selectedIndiId) Then
            MessageBox.Show("Selecione uma pessoa antes de abrir a árvore genealógica.", "Árvore genealógica", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim treeForm As New Form With {
            .Text = "🌳 Árvore genealógica — " & PersonName(selectedIndiId),
            .Width = 1000, .Height = 720, .MinimumSize = New Size(700, 500),
            .StartPosition = FormStartPosition.CenterParent
        }
        Dim tabs As New TabControl With {.Dock = DockStyle.Fill}
        Dim ancestorsTree = CreateGenealogyTree()
        Dim descendantsTree = CreateGenealogyTree()
        Dim relationsTree = CreateGenealogyTree()
        AddHandler ancestorsTree.NodeMouseDoubleClick, Sub(s, args) NavigateFromTree(args.Node, treeForm)
        AddHandler descendantsTree.NodeMouseDoubleClick, Sub(s, args) NavigateFromTree(args.Node, treeForm)
        AddHandler relationsTree.NodeMouseDoubleClick, Sub(s, args) NavigateFromTree(args.Node, treeForm)

        Dim rootId = selectedIndiId
        Dim ancestorRoot = MakePersonNode(rootId)
        ancestorsTree.Nodes.Add(ancestorRoot)
        AddAncestors(ancestorRoot, rootId, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), 0)
        ancestorRoot.Expand()

        Dim descendantRoot = MakePersonNode(rootId)
        descendantsTree.Nodes.Add(descendantRoot)
        AddDescendants(descendantRoot, rootId, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), 0)
        descendantRoot.Expand()

        Dim relationRoot = MakePersonNode(rootId)
        relationsTree.Nodes.Add(relationRoot)
        AddImmediateRelations(relationRoot, rootId)
        relationRoot.Expand()

        Dim ancestorsTab As New TabPage("👴 Ascendentes")
        ancestorsTab.Controls.Add(ancestorsTree)
        Dim descendantsTab As New TabPage("🌱 Descendentes")
        descendantsTab.Controls.Add(descendantsTree)
        Dim relationsTab As New TabPage("👨‍👩‍👧 Relações familiares")
        relationsTab.Controls.Add(relationsTree)
        tabs.TabPages.Add(ancestorsTab)
        tabs.TabPages.Add(descendantsTab)
        tabs.TabPages.Add(relationsTab)
        Dim help As New Label With {.Dock = DockStyle.Bottom, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 0, 0, 0), .Text = "Dica: dê duplo clique em uma pessoa para selecioná-la no editor."}
        treeForm.Controls.Add(tabs)
        treeForm.Controls.Add(help)
        treeForm.ShowDialog(Me)
    End Sub

    Private Function CreateGenealogyTree() As TreeView
        Return New TreeView With {
            .Dock = DockStyle.Fill, .HideSelection = False, .FullRowSelect = True,
            .ShowLines = True, .ShowPlusMinus = True, .ShowRootLines = True,
            .Font = New Font("Segoe UI", 10.0F), .Indent = 22
        }
    End Function

    Private Function MakePersonNode(id As String) As TreeNode
        If Not individuals.ContainsKey(id) Then Return New TreeNode("Pessoa inexistente: " & id)
        Dim ind = individuals(id)
        Dim caption = If(String.IsNullOrWhiteSpace(ind.Name), "(sem nome)", ind.Name) & "  [" & id & "]"
        Dim node As New TreeNode(caption) With {.Tag = id}
        Return node
    End Function

    Private Sub NavigateFromTree(node As TreeNode, treeForm As Form)
        If node Is Nothing OrElse node.Tag Is Nothing Then Return
        Dim id = TryCast(node.Tag, String)
        If id Is Nothing OrElse Not individuals.ContainsKey(id) Then Return
        SelectPerson(id)
        treeForm.Close()
    End Sub

    Private Sub AddAncestors(parentNode As TreeNode, personId As String, path As HashSet(Of String), depth As Integer)
        If depth >= 30 OrElse Not individuals.ContainsKey(personId) Then Return
        If path.Contains(personId) Then
            parentNode.Nodes.Add(New TreeNode("Ciclo de parentesco detectado"))
            Return
        End If
        Dim nextPath As New HashSet(Of String)(path, StringComparer.OrdinalIgnoreCase) From {personId}
        Dim ind = individuals(personId)
        If String.IsNullOrWhiteSpace(ind.Famc) OrElse Not families.ContainsKey(ind.Famc) Then
            parentNode.Nodes.Add(New TreeNode("👥🚫 Pais não registrados"))
            Return
        End If
        Dim fam = families(ind.Famc)
        Dim familyNode As New TreeNode("👨‍👩‍👧 Família parental " & fam.Id)
        parentNode.Nodes.Add(familyNode)
        Dim hasParent As Boolean = False
        For Each parentId In {fam.Husb, fam.Wife}
            If String.IsNullOrWhiteSpace(parentId) Then Continue For
            hasParent = True
            If individuals.ContainsKey(parentId) Then
                Dim personNode = MakePersonNode(parentId)
                familyNode.Nodes.Add(personNode)
                AddAncestors(personNode, parentId, nextPath, depth + 1)
            Else
                familyNode.Nodes.Add(New TreeNode("Referência inexistente: " & parentId))
            End If
        Next
        If Not hasParent Then familyNode.Nodes.Add(New TreeNode("👥🚫 Pais não informados"))
    End Sub

    Private Sub AddDescendants(parentNode As TreeNode, personId As String, path As HashSet(Of String), depth As Integer)
        If depth >= 30 Then
            parentNode.Nodes.Add(New TreeNode("Limite de gerações atingido"))
            Return
        End If
        If path.Contains(personId) Then
            parentNode.Nodes.Add(New TreeNode("Ciclo de parentesco detectado"))
            Return
        End If
        Dim nextPath As New HashSet(Of String)(path, StringComparer.OrdinalIgnoreCase) From {personId}
        Dim ind = individuals(personId)
        Dim foundFamily As Boolean = False
        For Each famId In ind.Fams.Distinct(StringComparer.OrdinalIgnoreCase)
            If Not families.ContainsKey(famId) Then Continue For
            foundFamily = True
            Dim fam = families(famId)
            Dim familyNode As New TreeNode("👨‍👩‍👧 Família " & fam.Id & If(fam.Events.Any(Function(ev) ev.Type = "MARR" AndAlso ev.DateText <> ""), " — casamento: " & fam.Events.First(Function(ev) ev.Type = "MARR" AndAlso ev.DateText <> "").DateText, ""))
            parentNode.Nodes.Add(familyNode)
            Dim spouseId = If(String.Equals(fam.Husb, personId, StringComparison.OrdinalIgnoreCase), fam.Wife, fam.Husb)
            If Not String.IsNullOrWhiteSpace(spouseId) Then
                If individuals.ContainsKey(spouseId) Then
                    Dim spouseNode = MakePersonNode(spouseId)
                    spouseNode.Text = "Cônjuge: 👥 " & spouseNode.Text
                    familyNode.Nodes.Add(spouseNode)
                Else
                    familyNode.Nodes.Add(New TreeNode("👤❌ Cônjuge com referência inexistente: " & spouseId))
                End If
            End If
            If fam.Children.Count = 0 Then familyNode.Nodes.Add(New TreeNode("👶🚫 Filhos não registrados"))
            For Each childId In fam.Children.Distinct(StringComparer.OrdinalIgnoreCase)
                If individuals.ContainsKey(childId) Then
                    Dim childNode = MakePersonNode(childId)
                    familyNode.Nodes.Add(childNode)
                    AddDescendants(childNode, childId, nextPath, depth + 1)
                Else
                    familyNode.Nodes.Add(New TreeNode("👶🚫Referência de filho inexistente: " & childId))
                End If
            Next
        Next
        If Not foundFamily Then parentNode.Nodes.Add(New TreeNode("👶🚫 Nenhum descendente registrado"))
    End Sub

    Private Sub AddImmediateRelations(root As TreeNode, personId As String)
        Dim ind = individuals(personId)
        Dim parentsNode As New TreeNode("👨👩‍🦰 Pais")
        root.Nodes.Add(parentsNode)
        If Not String.IsNullOrWhiteSpace(ind.Famc) AndAlso families.ContainsKey(ind.Famc) Then
            Dim fam = families(ind.Famc)
            If Not String.IsNullOrWhiteSpace(fam.Husb) Then parentsNode.Nodes.Add(If(individuals.ContainsKey(fam.Husb), MakePersonNode(fam.Husb), New TreeNode("👨‍🦳❌ Pai inexistente: " & fam.Husb)))
            If Not String.IsNullOrWhiteSpace(fam.Wife) Then parentsNode.Nodes.Add(If(individuals.ContainsKey(fam.Wife), MakePersonNode(fam.Wife), New TreeNode("👩‍🦰❌ Mãe inexistente: " & fam.Wife)))
            Dim siblingsNode As New TreeNode("👨‍👦‍👦 Irmãos")
            For Each siblingId In fam.Children.Where(Function(x) Not String.Equals(x, personId, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase)
                siblingsNode.Nodes.Add(If(individuals.ContainsKey(siblingId), MakePersonNode(siblingId), New TreeNode("👥🚫 Referência inexistente: " & siblingId)))
            Next
            If siblingsNode.Nodes.Count = 0 Then siblingsNode.Nodes.Add(New TreeNode("👥🚫 Nenhum irmão registrado"))
            parentsNode.Nodes.Add(siblingsNode)
        Else
            parentsNode.Nodes.Add(New TreeNode("👥❌ Pais não registrados"))
        End If

        Dim familiesNode As New TreeNode("👩‍👧‍👦 Cônjuges e filhos")
        root.Nodes.Add(familiesNode)
        For Each famId In ind.Fams.Distinct(StringComparer.OrdinalIgnoreCase)
            If Not families.ContainsKey(famId) Then Continue For
            Dim fam = families(famId)
            Dim famNode As New TreeNode("👨‍👩‍👧 Família " & fam.Id)
            familiesNode.Nodes.Add(famNode)
            Dim spouseId = If(String.Equals(fam.Husb, personId, StringComparison.OrdinalIgnoreCase), fam.Wife, fam.Husb)
            If Not String.IsNullOrWhiteSpace(spouseId) Then famNode.Nodes.Add(If(individuals.ContainsKey(spouseId), MakePersonNode(spouseId), New TreeNode("Cônjuge inexistente: " & spouseId)))
            Dim childrenNode As New TreeNode("👶Filhos")
            For Each childId In fam.Children.Distinct(StringComparer.OrdinalIgnoreCase)
                childrenNode.Nodes.Add(If(individuals.ContainsKey(childId), MakePersonNode(childId), New TreeNode("👶❌ Referência inexistente: " & childId)))
            Next
            If childrenNode.Nodes.Count = 0 Then childrenNode.Nodes.Add(New TreeNode("👶❌ Nenhum filho registrado"))
            famNode.Nodes.Add(childrenNode)
        Next
        If familiesNode.Nodes.Count = 0 Then familiesNode.Nodes.Add(New TreeNode("👩‍👧‍👦❌ Nenhum cônjuge ou filho registrado"))
    End Sub

    Private Sub AddField(table As TableLayoutPanel, caption As String, control As Control)
        Dim row = table.RowCount
        table.RowCount += 1
        table.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        table.Controls.Add(New Label With {.Text = caption, .AutoSize = True, .Anchor = AnchorStyles.Left, .Padding = New Padding(0, 7, 0, 0)}, 0, row)
        control.Margin = New Padding(3, 3, 3, 8)
        control.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        table.Controls.Add(control, 1, row)
    End Sub

    Private Function CellText(row As DataGridViewRow, index As Integer) As String
        Return If(row.Cells(index).Value Is Nothing, "", Convert.ToString(row.Cells(index).Value))
    End Function

    Private Function PersonName(id As String) As String
        If id Is Nothing Then Return "📝❓ Não informado"
        If individuals.ContainsKey(id) Then Return individuals(id).Name & " (" & id & ")"
        Return "📝❓ Não informado"
    End Function

    Private Sub AddIndividual()
        Dim id = GenerateUniqueId("I", individuals.Keys)
        individuals(id) = New Individual With {.Id = id, .Name = "👤 Novo indivíduo", .Sex = "U"}
        SelectPerson(id)
    End Sub

    Private Function GenerateUniqueId(prefix As String, keys As IEnumerable(Of String)) As String
        Dim setKeys As New HashSet(Of String)(keys, StringComparer.OrdinalIgnoreCase)
        Dim n = setKeys.Count + 1
        While setKeys.Contains($"@{prefix}{n}@")
            n += 1
        End While
        Return $"@{prefix}{n}@"
    End Function

    Private Sub DeleteIndividual(id As String)
        If MessageBox.Show("👤❌ Tem certeza de que deseja excluir esta pessoa?", "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
        For Each fam In families.Values
            If fam.Husb = id Then fam.Husb = Nothing
            If fam.Wife = id Then fam.Wife = Nothing
            fam.Children.RemoveAll(Function(x) x = id)
        Next
        individuals.Remove(id)
        CleanReferences()
        selectedIndiId = individuals.Keys.FirstOrDefault()
        RefreshPeopleList()
        RenderDetails()
    End Sub

    Private Sub AddParentFamily(indiId As String)
        Dim ind = individuals(indiId)
        If ind.Famc IsNot Nothing AndAlso families.ContainsKey(ind.Famc) Then
            MessageBox.Show("👨‍👩‍👧‍👦 Esta pessoa já possui uma família parental. Use Definir pai/mãe para editar o vínculo.")
            Return
        End If
        Dim id = GenerateUniqueId("F", families.Keys)
        families(id) = New Family With {.Id = id, .Children = New List(Of String) From {indiId}}
        ind.Famc = id
        RenderDetails()
    End Sub

    Private Sub SetParentInteractive(indiId As String, role As String)
        If Not individuals(indiId).Famc Is Nothing AndAlso families.ContainsKey(individuals(indiId).Famc) Then
            ChoosePerson("Selecionar " & If(role = "HUSB", "pai", "mãe"), {indiId}, Sub(parentId)
                                                                                        SetParent(indiId, parentId, role)
                                                                                    End Sub)
        Else
            AddParentFamily(indiId)
            SetParentInteractive(indiId, role)
        End If
    End Sub

    Private Sub SetParent(indiId As String, parentId As String, role As String)
        If indiId = parentId Then
            MessageBox.Show("🔄❌ Uma pessoa não pode ser seu próprio pai ou mãe.")
            Return
        End If
        Dim famId = individuals(indiId).Famc
        If famId Is Nothing OrElse Not families.ContainsKey(famId) Then Return
        Dim fam = families(famId)
        Dim oldId = If(role = "HUSB", fam.Husb, fam.Wife)
        If oldId IsNot Nothing AndAlso individuals.ContainsKey(oldId) Then individuals(oldId).Fams.RemoveAll(Function(x) x = famId)
        If role = "HUSB" Then fam.Husb = parentId Else fam.Wife = parentId
        If Not individuals(parentId).Fams.Contains(famId) Then individuals(parentId).Fams.Add(famId)
        RenderDetails()
    End Sub

    Private Sub UnlinkParent(indiId As String, role As String)
        Dim famId = individuals(indiId).Famc
        If famId Is Nothing OrElse Not families.ContainsKey(famId) Then Return
        Dim fam = families(famId)
        Dim oldId = If(role = "HUSB", fam.Husb, fam.Wife)
        If oldId IsNot Nothing AndAlso individuals.ContainsKey(oldId) Then individuals(oldId).Fams.RemoveAll(Function(x) x = famId)
        If role = "HUSB" Then fam.Husb = Nothing Else fam.Wife = Nothing
        CleanReferences()
        RenderDetails()
    End Sub

    Private Sub AddSpouseInteractive(indiId As String)
        ChoosePerson("👥 Selecionar cônjuge", {indiId}, Sub(spouseId)
                                                            Dim ind = individuals(indiId), spouse = individuals(spouseId)
                                                            Dim famId = GenerateUniqueId("F", families.Keys)
                                                            Dim fam As New Family With {.Id = famId, .Events = New List(Of GedEvent) From {New GedEvent With {.Type = "MARR"}}}
                                                            If ind.Sex = "F" Then
                                                                fam.Wife = ind.Id : fam.Husb = spouse.Id
                                                            Else
                                                                fam.Husb = ind.Id : fam.Wife = spouse.Id
                                                            End If
                                                            families(famId) = fam
                                                            ind.Fams.Add(famId) : spouse.Fams.Add(famId)
                                                            RenderDetails()
                                                        End Sub)
    End Sub

    Private Sub AddChildInteractive(famId As String)
        ChoosePerson("👦 Selecionar filho", {}, Sub(childId)
                                                    Dim child = individuals(childId)
                                                    If child.Famc IsNot Nothing AndAlso child.Famc <> famId Then
                                                        MessageBox.Show("👨‍👩‍👧  Esta pessoa já possui outra família parental.")
                                                        Return
                                                    End If
                                                    child.Famc = famId
                                                    If Not families(famId).Children.Contains(childId) Then families(famId).Children.Add(childId)
                                                    RenderDetails()
                                                End Sub)
    End Sub

    Private Sub ChoosePerson(title As String, excluded As IEnumerable(Of String), callback As Action(Of String))
        Dim excludedSet As New HashSet(Of String)(excluded, StringComparer.OrdinalIgnoreCase)
        Dim choices = individuals.Values.Where(Function(x) Not excludedSet.Contains(x.Id)).OrderBy(Function(x) x.Name).ToList()
        If choices.Count = 0 Then
            MessageBox.Show("👤❌ Não há outras pessoas disponíveis.")
            Return
        End If
        Using dlg As New Form With {.Text = title, .Width = 480, .Height = 150, .StartPosition = FormStartPosition.CenterParent, .FormBorderStyle = FormBorderStyle.FixedDialog, .MinimizeBox = False, .MaximizeBox = False}
            Dim combo As New ComboBox With {.Dock = DockStyle.Top, .DropDownStyle = ComboBoxStyle.DropDownList}
            For Each person In choices
                combo.Items.Add(New PersonListItem(person.Id, $"{person.Name} ({person.Id})"))
            Next
            combo.SelectedIndex = 0
            Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 42, .FlowDirection = FlowDirection.RightToLeft}
            Dim ok As New Button With {.Text = "📝 Selecionar", .DialogResult = DialogResult.OK, .AutoSize = True}
            Dim cancel As New Button With {.Text = "❌ Cancelar", .DialogResult = DialogResult.Cancel, .AutoSize = True}
            buttons.Controls.AddRange({ok, cancel})
            dlg.Controls.Add(combo) : dlg.Controls.Add(buttons)
            dlg.AcceptButton = ok : dlg.CancelButton = cancel
            If dlg.ShowDialog(Me) = DialogResult.OK AndAlso TypeOf combo.SelectedItem Is PersonListItem Then
                callback(DirectCast(combo.SelectedItem, PersonListItem).Id)
            End If
        End Using
    End Sub

    Private Sub UnlinkSpouse(famId As String)
        If MessageBox.Show("💍❌ Desvincular o casal? Os filhos permanecerão na família.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        If Not families.ContainsKey(famId) Then Return
        Dim fam = families(famId)
        If fam.Husb IsNot Nothing AndAlso individuals.ContainsKey(fam.Husb) Then individuals(fam.Husb).Fams.RemoveAll(Function(x) x = famId)
        If fam.Wife IsNot Nothing AndAlso individuals.ContainsKey(fam.Wife) Then individuals(fam.Wife).Fams.RemoveAll(Function(x) x = famId)
        fam.Husb = Nothing : fam.Wife = Nothing
        CleanReferences()
        RenderDetails()
    End Sub

    Private Sub SaveGedcom(ancestorsOnly As Boolean, extended As Boolean)
        Try
            Dim content = GenerateGedcom(ancestorsOnly, extended)
            Using dlg As New SaveFileDialog With {
                .Filter = "Arquivo GEDCOM (*.ged)|*.ged",
                .DefaultExt = "ged",
                .AddExtension = True,
                .FileName = If(extended, "ascendentes_filhos_familias.ged", If(ancestorsOnly, "ascendentes_I1.ged", "arvore_genealogica.ged")),
                .Title = "💾 Salvar arquivo GEDCOM"
            }
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                File.WriteAllText(dlg.FileName, content, New UTF8Encoding(False))
                statusLabel.Text = "💾 Arquivo salvo: " & dlg.FileName
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Erro ao exportar", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function GenerateGedcom(ancestorsOnly As Boolean, includeAncestorsFamilies As Boolean) As String
        CleanReferences()
        Dim allowedIndividuals As HashSet(Of String) = Nothing
        Dim allowedFamilies As HashSet(Of String) = Nothing

        If ancestorsOnly OrElse includeAncestorsFamilies Then
            If Not individuals.ContainsKey("@I1@") Then Throw New InvalidOperationException("O indivíduo @I1@ não foi encontrado no arquivo GEDCOM.")
            allowedIndividuals = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"@I1@"}
            allowedFamilies = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim pending As New Stack(Of String)()
            pending.Push("@I1@")
            While pending.Count > 0
                Dim indiId = pending.Pop()
                Dim ind = individuals(indiId)
                If ind.Famc Is Nothing OrElse Not families.ContainsKey(ind.Famc) Then Continue While
                Dim famId = ind.Famc
                allowedFamilies.Add(famId)
                Dim fam = families(famId)
                For Each parentId In {fam.Husb, fam.Wife}
                    If parentId IsNot Nothing AndAlso individuals.ContainsKey(parentId) AndAlso allowedIndividuals.Add(parentId) Then pending.Push(parentId)
                Next
            End While

            If includeAncestorsFamilies Then
                Dim ancestorIds = New HashSet(Of String)(allowedIndividuals, StringComparer.OrdinalIgnoreCase)
                For Each famId In allowedFamilies.ToList()
                    If families.ContainsKey(famId) Then
                        For Each childId In families(famId).Children
                            If individuals.ContainsKey(childId) Then allowedIndividuals.Add(childId)
                        Next
                    End If
                Next
                For Each ancestorId In ancestorIds
                    Dim ancestor = individuals(ancestorId)
                    For Each famId In ancestor.Fams
                        If Not families.ContainsKey(famId) Then Continue For
                        allowedFamilies.Add(famId)
                        For Each childId In families(famId).Children
                            If individuals.ContainsKey(childId) Then allowedIndividuals.Add(childId)
                        Next
                    Next
                Next
                Dim childrenOfAncestors As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                For Each ancestorId In ancestorIds
                    For Each famId In individuals(ancestorId).Fams
                        If families.ContainsKey(famId) Then
                            For Each childId In families(famId).Children
                                childrenOfAncestors.Add(childId)
                            Next
                        End If
                    Next
                Next
                For Each childId In childrenOfAncestors
                    If Not individuals.ContainsKey(childId) Then Continue For
                    For Each famId In individuals(childId).Fams
                        If Not families.ContainsKey(famId) Then Continue For
                        Dim fam = families(famId)
                        allowedFamilies.Add(famId)
                        For Each relativeId In New String() {fam.Husb, fam.Wife}.Concat(fam.Children)
                            If relativeId IsNot Nothing AndAlso individuals.ContainsKey(relativeId) Then allowedIndividuals.Add(relativeId)
                        Next
                    Next
                Next
            End If
        End If

        Dim output As New List(Of String) From {"0 HEAD", "1 SOUR GEDCOM_EDITOR_VB", "1 GEDC", "2 VERS 5.5.1", "2 FORM LINEAGE-LINKED", "1 CHAR UTF-8"}
        For Each ind In individuals.Values
            If allowedIndividuals IsNot Nothing AndAlso Not allowedIndividuals.Contains(ind.Id) Then Continue For
            output.Add($"0 {ind.Id} INDI")
            output.Add("1 NAME " & If(String.IsNullOrWhiteSpace(ind.Name), "Desconhecido", FormatGedcomName(ind.Name)))
            If Not String.IsNullOrWhiteSpace(ind.Sex) Then output.Add("1 SEX " & ind.Sex)
            For Each ev In ind.Events
                output.Add($"1 {ev.Type}{If(ev.Description <> "", " " & ev.Description, "")}")
                If ev.DateText <> "" Then output.Add("2 DATE " & ev.DateText)
                If ev.Place <> "" Then output.Add("2 PLAC " & ev.Place)
            Next
            If ind.Famc IsNot Nothing AndAlso (allowedFamilies Is Nothing OrElse allowedFamilies.Contains(ind.Famc)) Then output.Add("1 FAMC " & ind.Famc)
            For Each famId In ind.Fams
                If allowedFamilies Is Nothing OrElse allowedFamilies.Contains(famId) Then output.Add("1 FAMS " & famId)
            Next
        Next
        For Each fam In families.Values
            If allowedFamilies IsNot Nothing AndAlso Not allowedFamilies.Contains(fam.Id) Then Continue For
            output.Add($"0 {fam.Id} FAM")
            If fam.Husb IsNot Nothing AndAlso (allowedIndividuals Is Nothing OrElse allowedIndividuals.Contains(fam.Husb)) Then output.Add("1 HUSB " & fam.Husb)
            If fam.Wife IsNot Nothing AndAlso (allowedIndividuals Is Nothing OrElse allowedIndividuals.Contains(fam.Wife)) Then output.Add("1 WIFE " & fam.Wife)
            For Each childId In fam.Children
                If allowedIndividuals Is Nothing OrElse allowedIndividuals.Contains(childId) Then output.Add("1 CHIL " & childId)
            Next
            For Each ev In fam.Events
                output.Add("1 " & ev.Type)
                If ev.DateText <> "" Then output.Add("2 DATE " & ev.DateText)
                If ev.Place <> "" Then output.Add("2 PLAC " & ev.Place)
            Next
        Next
        output.Add("0 TRLR")
        Return String.Join(vbCrLf, output)
    End Function

    Private Function FormatGedcomName(name As String) As String
        ' O modelo interno armazena o nome sem barras. Recoloca barras no último sobrenome
        ' para produzir um NAME compatível com a convenção habitual do GEDCOM.
        Dim parts = name.Trim().Split(" "c, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length <= 1 Then Return name.Trim()
        Return String.Join(" ", parts.Take(parts.Length - 1)) & " /" & parts(parts.Length - 1) & "/"
    End Function
End Class
