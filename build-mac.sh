#!/bin/bash

# Script to build distributable application for macOS
# RT880-FlasherX Build Script

set -e  # Exit on errors

echo "🔨 Building RT880-FlasherX for macOS..."

# Clean previous builds
echo "🧹 Cleaning previous builds..."
dotnet clean

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Build for macOS (arm64)
echo "🏗️  Building for macOS ARM64..."
dotnet publish RT880-FlasherX/RT880-FlasherX.csproj \
    --configuration Release \
    --runtime osx-arm64 \
    --self-contained true \
    --output ./dist/osx-arm64

# Build for macOS (x64) - Intel Macs
echo "🏗️  Building for macOS x64..."
dotnet publish RT880-FlasherX/RT880-FlasherX.csproj \
    --configuration Release \
    --runtime osx-x64 \
    --self-contained true \
    --output ./dist/osx-x64

# Create app bundles for macOS
echo "📦 Creating app bundles..."

# Function to create app bundle
create_app_bundle() {
    local arch=$1
    local app_name="RT880-FlasherX.app"
    local app_path="./dist/$arch/$app_name"
    
    echo "🍎 Creating $app_name for $arch..."
    
    # Create app bundle directory structure
    mkdir -p "$app_path/Contents/MacOS"
    mkdir -p "$app_path/Contents/Resources"
    
    # Move executable to MacOS folder
    mv "./dist/$arch/RT880-FlasherX" "$app_path/Contents/MacOS/RT880-FlasherX"
    
    # Move ALL files to MacOS directory for self-contained .NET apps
    find "./dist/$arch" -mindepth 1 -maxdepth 1 -not -name "*.app" -exec mv {} "$app_path/Contents/MacOS/" \;
    
    # Copy Info.plist
    cp "./Info.plist" "$app_path/Contents/"
    
    # Make executable executable
    chmod +x "$app_path/Contents/MacOS/RT880-FlasherX"
    
    echo "✅ App bundle created: $app_path"
}

# Create app bundles for both architectures
create_app_bundle "osx-arm64"
create_app_bundle "osx-x64"

echo "✅ Build completed!"
echo "📁 Files generated:"
echo "   ARM64 executable: ./dist/osx-arm64/RT880-FlasherX"
echo "   ARM64 app bundle: ./dist/osx-arm64/RT880-FlasherX.app"
echo "   x64 executable:   ./dist/osx-x64/RT880-FlasherX"
echo "   x64 app bundle:   ./dist/osx-x64/RT880-FlasherX.app"
echo ""
echo "💡 To run:"
echo "   Executable: ./dist/osx-arm64/RT880-FlasherX"
echo "   App bundle: open ./dist/osx-arm64/RT880-FlasherX.app"
echo "💡 To install:"
echo "   Drag RT880-FlasherX.app to Applications folder"