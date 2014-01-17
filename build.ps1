Properties {
    $base_version = "3.0.0"
    $pre_release = "local"
    $build_number = Get-BuildNumber
    $config = "Release"

	$git_commit = Get-GitCommit
    $installer_product_id = New-Object System.Guid($git_commit.Hash.SubString(0,32))
    $installer_upgrade_code = New-Object System.Guid($git_commit.Hash.SubString(1,32))

    $base_dir = Split-Path $psake.build_script_file 
	$src_dir = "$base_dir"
	$tools_dir = "$base_dir\tools"
	$artifacts_dir = "$base_dir\artifacts"
	$bin_dir = "$artifacts_dir\bin"
	$40_bin_dir = "$bin_dir\net40"
	$45_bin_dir = "$bin_dir\net45"
	$test_results_dir = "$artifacts_dir\test_results"
	$docs_dir = "$artifacts_dir\docs"

	$sln_file = "$base_dir\CSharpDriver.sln"
	$asm_file = "$src_dir\GlobalAssemblyInfo.cs"
	$version_file = "$artifacts_dir\version.txt"
	$docs_file = "$base_dir\Docs\Api\CSharpDriverDocs.shfbproj"
	$installer_file = "$base_dir\Installer\CSharpDriverInstaller.wixproj"
	$nuspec_file = "$base_dir\mongocsharpdriver.nuspec"
    $nuspec_build_file = "$base_dir\mongocsharpdriverbuild.nuspec"
	$license_file = "$base_dir\License.txt"

	$nuget_tool = "$tools_dir\nuget\nuget.exe"
	$nunit_tool = "$tools_dir\nunit\nunit-console.exe"
	$opencover_tool = "$tools_dir\OpenCover\OpenCover.Console.exe"
	$coberturaconverter_tool = "$tools_dir\OpenCoverToCoberturaConverter\Palmmedia.OpenCoverToCoberturaConverter.exe"
	$reportgenerator_tool = "$tools_dir\ReportGenerator\bin\ReportGenerator.exe"
	$zip_tool = "$tools_dir\7Zip\7za.exe"
}

function IsReleaseBuild {
    if($pre_release -eq "build" -or $pre_release -eq "local") {
        return $false
    }

    return $true
}

TaskSetup {

    $global:version = "$base_version.$build_number"
    $global:sem_version = $base_version
    if(-not [string]::IsNullOrEmpty($pre_release)) {
        $global:sem_version = "$sem_version-$($pre_release)"

        if(-not (IsReleaseBuild)) {
            # These should be + instead of -, but nuget doesn't allow that right now
            # Also padding the build number because nuget sorts lexigraphically
            # meaning that 2 > 10.  So, we make it such that 0002 < 0010.
            # Note: will we ever have > 9999 commits between releases?
            # Note: 0 + $build_number is to coerce the build_number into an integer.
            $bn = "{0:0000}" -f (0 + $build_number)
            $global:sem_version = "$sem_version-$bn"
        }
    }

    Write-Host "$config Version $sem_version($version)" -ForegroundColor Yellow

    $global:chm_file = "$artifacts_dir\CSharpDriverDocs-$sem_version.chm"
    $global:release_notes_file = "$base_dir\Release Notes\Release Notes v$base_version.md"
}

Framework('4.0')

Include tools\psake\psake-ext.ps1

function BuildHasBeenRun {
	$build_exists = (Test-Path $40_bin_dir) -and (Test-Path $45_bin_dir)
	Assert $build_exists "Build task has not been run"
	$true
}

function DocsHasBeenRun {
	$build_exists = Test-Path $chm_file
	Assert $build_exists "Docs task has not been run"
	$true
}

function NotLocalPreRelease {
    $is_not_local = $pre_release -ne "local"
    Assert $is_not_local "Cannot run task on a local build. Specify a different (or none) pre-release version."
    $true
}

Task Default -Depends Build

Task OutputVersion {
    if(-not (Test-Path $artifacts_dir)) {
        mkdir -path $artifacts_dir | out-null
    }

    $Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding($False)

    Write-Host "Writing version file to $version_file" -ForegroundColor Green
    $lines = @("BUILD_VERSION=$base_version";"BUILD_PRE_RELEASE=$pre_release";"BUILD_NUMBER=$build_number";"BUILD_SEM_VERSION=$sem_version")
    [System.IO.File]::WriteAllLines($version_file, $lines, $Utf8NoBomEncoding)
}

Task Clean {
	RemoveDirectory $artifacts_dir
	
	Write-Host "Cleaning $sln_file" -ForegroundColor Green
	Exec { msbuild "$sln_file" /t:Clean /p:Configuration=".NET 4.0 - $config" /v:quiet } 
	Exec { msbuild "$sln_file" /t:Clean /p:Configuration=".NET 4.5 - $config" /v:quiet } 
}

Task Build -Depends Clean, OutputVersion {
	try {
		Generate-AssemblyInfo `
			-file $asm_file `
			-version $version `
			-config $config `
			-sem_version $sem_version `

		mkdir -path $40_bin_dir | out-null
		Write-Host "Building $sln_file for .NET 4.0" -ForegroundColor Green
		Exec { msbuild "$sln_file" /t:Rebuild /p:Configuration=".NET 4.0 - $config" /p:TargetFrameworkVersion=v4.0 /v:quiet /p:OutDir=$40_bin_dir }

		mkdir -path $45_bin_dir | out-null
		Write-Host "Building $sln_file for .NET 4.5" -ForegroundColor Green
		Exec { msbuild "$sln_file" /t:Rebuild /p:Configuration=".NET 4.5 - $config" /p:TargetFrameworkVersion=v4.5 /v:quiet /p:OutDir=$45_bin_dir }
	}
	finally {
		Reset-AssemblyInfo
	}
}

Task Test -precondition { BuildHasBeenRun } {
	RemoveDirectory $test_results_dir
	mkdir -path $test_results_dir | out-null

	$test_assemblies = ls -rec $40_bin_dir\*Tests*.dll
	Write-Host "Testing $test_assemblies for .NET 4.0" -ForegroundColor Green
	Exec { &$nunit_tool $test_assemblies /timeout=10000 /xml=$test_results_dir\net40-test-results.xml /framework=net-4.0 /nologo /noshadow }

	$test_assemblies = ls -rec $45_bin_dir\*Tests*.dll
	Write-Host "Testing $test_assemblies for .NET 4.5" -ForegroundColor Green
	Exec { &$nunit_tool $test_assemblies /timeout=10000 /xml=$test_results_dir\net45-test-results.xml /framework=net-4.0 /nologo /noshadow }
}

Task TestWithCoverage -precondition { BuildHasBeenRun } {
	RemoveDirectory $test_results_dir
	mkdir -path $test_results_dir | out-null

	$test_assemblies = ls -rec $40_bin_dir\*Tests*.dll
	Write-Host "Testing $test_assemblies for .NET 4.0 with Code Coverage" -ForegroundColor Green
	$nunit_args = "$test_assemblies /timeout=10000 /xml=$test_results_dir\net40-test-results.xml /framework=net-4.0 /nologo /noshadow"
	Exec { &$opencover_tool "-register:user" "-target:$nunit_tool" "-targetargs:$nunit_args" "-filter:+[MongoDB*]* -[MongoDB*Tests*]*" "-output:$test_results_dir\net40-test-coverage.xml" }

	$test_assemblies = ls -rec $45_bin_dir\*Tests*.dll
	Write-Host "Testing $test_assemblies for .NET 4.5 with Code Coverage" -ForegroundColor Green
	$nunit_args = "$test_assemblies /timeout=10000 /xml=$test_results_dir\net45-test-results.xml /framework=net-4.0 /nologo /noshadow"
	Exec { &$opencover_tool "-register:user" "-target:$nunit_tool" "-targetargs:$nunit_args" "-filter:+[MongoDB*]* -[MongoDB*Tests*]*" "-output:$test_results_dir\net45-test-coverage.xml" }

	Exec { &$coberturaconverter_tool "-input:$test_results_dir\net40-test-coverage.xml" "-output:$test_results_dir\net40-cobertura-coverage.xml" "-sources:$src_dir" }

	Exec { &$coberturaconverter_tool "-input:$test_results_dir\net45-test-coverage.xml" "-output:$test_results_dir\net45-covertura-coverage.xml" "-sources:$src_dir" }

	Exec { &$reportgenerator_tool "-reports:$test_results_dir\*-test-coverage.xml" "-targetdir:$test_results_dir\report" "-reporttypes:Html;HtmlSummary" }
}

Task Docs -precondition { BuildHasBeenRun } {
	RemoveDirectory $docs_dir

	mkdir -path $docs_dir | out-null
	Exec { msbuild "$docs_file" /p:Configuration=$config /p:CleanIntermediate=True /p:HelpFileVersion=$version /p:OutputPath=$docs_dir } 

	mv "$docs_dir\CSharpDriverDocs.chm" $chm_file
	mv "$docs_dir\Index.html" "$docs_dir\index.html"
	Exec { &$zip_tool a "$artifacts_dir\CSharpDriverDocs-$sem_version-html.zip" "$docs_dir\*" }
	RemoveDirectory $docs_dir
}

Task Zip -precondition { (BuildHasBeenRun) -and (DocsHasBeenRun) }{
	$zip_dir = "$artifacts_dir\ziptemp"
	
	RemoveDirectory $zip_dir
	mkdir -path $zip_dir | out-null

	mkdir -path "$zip_dir\net40" | out-null
	$40_items = @("$40_bin_dir\MongoDB.Bson.dll", `
		"$40_bin_dir\MongoDB.Bson.pdb", `
		"$40_bin_dir\MongoDB.Bson.xml", `
		"$40_bin_dir\MongoDB.Driver.Core.dll", `
		"$40_bin_dir\MongoDB.Driver.Core.pdb", `
		"$40_bin_dir\MongoDB.Driver.Core.xml", `
		"$40_bin_dir\MongoDB.Driver.dll", `
		"$40_bin_dir\MongoDB.Driver.pdb", `
		"$40_bin_dir\MongoDB.Driver.xml")
	cp $40_items "$zip_dir\net40"

	mkdir -path "$zip_dir\net45" | out-null
	$45_items = @("$45_bin_dir\MongoDB.Bson.dll", `
		"$45_bin_dir\MongoDB.Bson.pdb", `
		"$45_bin_dir\MongoDB.Bson.xml", `
		"$45_bin_dir\MongoDB.Driver.Core.dll", `
		"$45_bin_dir\MongoDB.Driver.Core.pdb", `
		"$45_bin_dir\MongoDB.Driver.Core.xml", `
		"$45_bin_dir\MongoDB.Driver.dll", `
		"$45_bin_dir\MongoDB.Driver.pdb", `
		"$45_bin_dir\MongoDB.Driver.xml")
	cp $45_items "$zip_dir\net45"

	cp $license_file $zip_dir
	cp "Release Notes\Release Notes v$base_version.md" "$zip_dir\Release Notes.txt"
	cp $chm_file "$zip_dir\CSharpDriverDocs.chm"

	Exec { &$zip_tool a "$artifacts_dir\CSharpDriver-$sem_version.zip" "$zip_dir\*" }

	rd $zip_dir -rec -force | out-null
}

Task Installer -precondition { (BuildHasBeenRun) -and (DocsHasBeenRun) } {
	$release_notes_relative_path = Get-Item $release_notes_file | Resolve-Path -Relative
	$doc_relative_path = Get-Item $chm_file | Resolve-Path -Relative

	Exec { msbuild "$installer_file" /t:Rebuild /p:Configuration=$config /p:Version=$version /p:SemVersion=$sem_version /p:ProductId=$installer_product_id /p:UpgradeCode=$installer_upgrade_code /p:ReleaseNotes=$release_notes_relative_path /p:License="License.rtf" /p:Documentation=$doc_relative_path /p:OutputPath=$artifacts_dir /p:BinDir=$bin_dir}
	
	rm -force $artifacts_dir\*.wixpdb
}

Task NugetPack -precondition { (NotLocalPreRelease) -and (BuildHasBeenRun) -and (DocsHasBeenRun) } {

    $nf = $nuspec_file
    if($pre_release -eq "build") {
        $nf = $nuspec_build_file
    }

	Exec { &$nuget_tool pack $nf -o $artifacts_dir -Version $sem_version -Symbols -BasePath $base_dir }
}