rimworld := "/media/scarf/@steam/SteamLibrary/steamapps/common/RimWorld"
config := "/home/scarf/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Config/ModsConfig.xml"
legacy_ref := env("RIMWORLD_1_6_REF", "rimworld-1.6")
legacy_dir := "Source/obj/rimworld-1.6"
legacy_managed := env("RIMWORLD_1_6_MANAGED", "/media/scarf/@steam/RimWorld-1.6.4871-backup/RimWorldLinux_Data/Managed")
mod_name := "GoblinsDontDeserveGenevaConvention"
package_id := "scarf.goblin.exceptionalism"
mod_dest := rimworld + "/Mods/" + mod_name
project := "Source/GDGC/GDGC.csproj"

# Format C# source
fmt:
    mise exec dotnet@10.0.301 -- dotnet format ./{{project}}

# Build the RimWorld 1.7 and frozen 1.6 assemblies
build: build-17 build-16

# Build the RimWorld 1.7 assembly
build-17:
    mise exec dotnet@10.0.301 -- dotnet build ./{{project}} -c Release

# Build the RimWorld 1.6 assembly from the frozen 1.6 branch
build-16:
    mkdir -p {{legacy_dir}}
    git archive {{legacy_ref}} Source | tar -x -C {{legacy_dir}}
    mise exec dotnet@10.0.301 -- dotnet build {{legacy_dir}}/{{project}} -c Release -p:RimWorldManagedDir="{{legacy_managed}}" -p:OutputPath="$PWD/1.6/Assemblies/"

# Install built mod to RimWorld Mods directory
install: build
    @pgrep -x 'RimWorldLinux|RimWorld' >/dev/null 2>&1 && { echo "RimWorld is running — 설치 전에 종료해주세요"; exit 1; } || true
    test -f 1.6/Assemblies/GDGC.dll
    test -f 1.7/Assemblies/GDGC.dll
    rm -rf "{{mod_dest}}"
    mkdir -p "{{mod_dest}}/1.6/Assemblies" "{{mod_dest}}/1.7/Assemblies"
    cp 1.6/Assemblies/GDGC.dll "{{mod_dest}}/1.6/Assemblies/"
    cp 1.7/Assemblies/GDGC.dll "{{mod_dest}}/1.7/Assemblies/"
    rsync -a --exclude '*~' About Defs Languages Textures "{{mod_dest}}/"
    @echo "Installed to {{mod_dest}}"

# Install and enable in ModsConfig.xml
enable: install
    @if [ -f "{{config}}" ]; then \
        if ! rg -q '<li>{{package_id}}</li>' "{{config}}"; then \
            sed -i 's|</activeMods>|  <li>{{package_id}}</li>\n</activeMods>|' "{{config}}"; \
            echo "Added {{package_id}} to activeMods"; \
        else \
            echo "{{package_id}} already in activeMods"; \
        fi; \
    else \
        echo "ModsConfig.xml not found at {{config}}"; \
        exit 1; \
    fi

# Remove build outputs
clean:
    rm -rf Source/GDGC/bin Source/GDGC/obj
    rm -f 1.6/Assemblies/GDGC.dll 1.7/Assemblies/GDGC.dll
