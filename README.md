# Infinite Flat Terrain 

A real-time height map based terrain renderer with LOD, made using Unity. 
<br/><br/>
![Close and far example of the terrain](https://github.com/roopekt/InfiniteFlatTerrain/blob/main/ReadmeData/close_and_far_example.png)

## Installation 

If you only need the binaries, you can get them [here](https://github.com/roopekt/InfiniteFlatTerrain/releases). Otherwise:

 1. Clone the repository
	```shell
	git clone https://github.com/roopekt/InfiniteFlatTerrain
	```
2. Install Visual Studio
	https://visualstudio.microsoft.com/downloads/
3. Install Unity
	- Install Unity Hub from https://unity3d.com/get-unity/download
	- Using the hub, install  an editor with the version 2020.3.15f2 (LTS). Remember to check visual studio integration when prompted about additional packages.
4. Locate and open the project trough Unity Hub
5. Install Free HDR Sky to the project
	- The asset is available for free on the asset store [here](https://assetstore.unity.com/packages/2d/textures-materials/sky/free-hdr-sky-61217)
	- The asset must be installed separately as the license doesn't allow to distribute it as a part of an open source project

## Usage 

 - Turn camera by dragging with mouse
 - Move with w, a, s, d, shift and space keys
 - adjust speed with the scroll wheel
 - close by pressing esc

## How it works 

1. The terrain is a big circular disk tiled in a simple manner 
<br/><br/>
![Tiled disk](https://github.com/roopekt/InfiniteFlatTerrain/blob/main/ReadmeData/tiled_disk.png)
2. Vertex positions are periodically rendered into a multibuffered texture using a compute shader. Height is calculated using Perlin noise with multiple layers.
3. Normals are also generated by a compute shader.
4. A vertex shader reads the vertex positions and normals from the textures.
5. A fragment shader decides the color of each pixel based on height and slope.
6. Water is just a simple transparent plane.

## License 

This project is distributed under the MIT License. See `LICENSE.txt` for more information.

## Acknowledgments

- The skybox is part of [Free HDR Sky](https://assetstore.unity.com/packages/2d/textures-materials/sky/free-hdr-sky-61217) by ProAssets

<br/><br/>
![Mountain with a lake behind it](https://github.com/roopekt/InfiniteFlatTerrain/blob/main/ReadmeData/mountain.png) 
