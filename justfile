rimworld := "/media/scarf/@steam/SteamLibrary/steamapps/common/RimWorld"
config := "/home/scarf/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Config/ModsConfig.xml"
steam_os := "RimWorldLinux_Data"
mod_name := "GoblinsDontDeserveGenevaConvention"
package_id := "local.goblinsdontdeservegenevaconvention"
mod_dest := rimworld + "/Mods/" + mod_name
project := "Source/GDGC/GDGC.csproj"
assembly := "1.6/Assemblies/GDGC.dll"

# Format C# source
fmt:
    STEAM_APPS="{{rimworld}}/../.." \
    STEAM_OS="{{steam_os}}" \
    mise exec dotnet@8.0.422 -- dotnet format ./{{project}}

# Build the C# mod
build-dll:
    STEAM_APPS="{{rimworld}}/../.." \
    STEAM_OS="{{steam_os}}" \
    mise exec dotnet@8.0.422 -- dotnet build ./{{project}} -c Release

# Build everything
build: build-dll

# Install built mod to RimWorld Mods directory
install: build
    @pgrep -x RimWorldLinux >/dev/null 2>&1 && { echo "RimWorld is running — 설치 전에 종료해주세요"; exit 1; } || true
    test -f "{{assembly}}"
    rm -rf "{{mod_dest}}"
    mkdir -p "{{mod_dest}}/1.6/Assemblies"
    cp "{{assembly}}" "{{mod_dest}}/1.6/Assemblies/GDGC.dll"
    cp -r About "{{mod_dest}}/"
    cp -r Defs "{{mod_dest}}/"
    cp -r Languages "{{mod_dest}}/"
    cp -r Textures "{{mod_dest}}/"
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
    rm -f "{{assembly}}"
