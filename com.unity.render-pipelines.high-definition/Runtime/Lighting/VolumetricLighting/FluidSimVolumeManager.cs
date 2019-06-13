using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class FluidSimVolumeManager
    {
        static private FluidSimVolumeManager _instance = null;

        public static FluidSimVolumeManager manager
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FluidSimVolumeManager();
                }
                return _instance;
            }
        }

        private ComputeShader _fluidSimVolumeCS = null;
        private ComputeShader _texture3DAtlasCS = null;

        private List<FluidSimVolume> _volumes = null;

        public RTHandleSystem.RTHandle volumeAtlas = null;
        private bool atlasNeedsRefresh = false;

        //TODO: hardcoded size....:-(
        public static int fluidSimVolumeTextureSize = 256;

        private FluidSimVolumeManager()
        {
            int res = 512;

            _volumes = new List<FluidSimVolume>();
            volumeAtlas = RTHandles.Alloc(
                res,
                res,
                res,
                colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
                filterMode: FilterMode.Bilinear,
                dimension: TextureDimension.Tex3D,
                enableRandomWrite: true,
                name: "FluidSimVolumeAtlas");
        }

        public void Build(HDRenderPipelineAsset asset)
        {
            _fluidSimVolumeCS = asset.renderPipelineResources.shaders.fluidSimVolumeCS;
            _texture3DAtlasCS = asset.renderPipelineResources.shaders.texture3DAtlasCS;
        }

        public void RegisterVolume(FluidSimVolume volume)
        {
            _volumes.Add(volume);
        }

        public void DeRegisterVolume(FluidSimVolume volume)
        {
            if (_volumes.Contains(volume))
            {
                _volumes.Remove(volume);
            }
        }

        public void SimulateVolume(CommandBuffer cmd)
        {
            foreach (var volume in _volumes)
            {
                var initialSimTexture = volume.parameters.initialStateTexture;
                if (initialSimTexture == null)
                    continue;

                int fluidSimVolumeResX = initialSimTexture.width;
                int fluidSimVolumeResY = initialSimTexture.height;
                int fluidSimVolumeResZ = initialSimTexture.depth;

                const int threadTile = 4;
                const int lessTile = threadTile - 1;

                int dispatchX = (fluidSimVolumeResX + lessTile) / threadTile;
                int dispatchY = (fluidSimVolumeResY + lessTile) / threadTile;
                int dispatchZ = (fluidSimVolumeResZ + lessTile) / threadTile;

                if (volume.needToInitialize)
                {
                    var outputVolumeTexture = volume.fSimTexture;
                    if (outputVolumeTexture == null)
                        continue;

                    var kernel = _fluidSimVolumeCS.FindKernel("InitialState");
                    if (kernel == -1)
                        continue;

                    cmd.SetComputeTextureParam(_fluidSimVolumeCS, kernel, HDShaderIDs._InputVolumeTexture, initialSimTexture);
                    cmd.SetComputeTextureParam(_fluidSimVolumeCS, kernel, HDShaderIDs._OutputVolumeTexture, outputVolumeTexture);

                    cmd.DispatchCompute(_fluidSimVolumeCS, kernel, dispatchX, dispatchY, dispatchZ);
                }
                else
                {
                    var inputVolumeTexture = volume.fSimTexture;
                    if (inputVolumeTexture == null)
                        continue;
                
                    var outputVolumeTexture = volume.bSimTexture;
                    if (outputVolumeTexture == null)
                        continue;
                
                    var kernel = _fluidSimVolumeCS.FindKernel("Simulate");
                    if (kernel == -1)
                        continue;
                
                    volume.SwapTexture();
                
                    var fluidSimVolumeRes = new Vector3(fluidSimVolumeResX, fluidSimVolumeResY, fluidSimVolumeResZ);
                
                    cmd.SetComputeVectorParam(_fluidSimVolumeCS, HDShaderIDs._FluidSimVolumeRes, fluidSimVolumeRes);
                
                    cmd.SetComputeTextureParam(_fluidSimVolumeCS, kernel, HDShaderIDs._InputVolumeTexture, inputVolumeTexture);
                    cmd.SetComputeTextureParam(_fluidSimVolumeCS, kernel, HDShaderIDs._OutputVolumeTexture, outputVolumeTexture);
                
                    cmd.DispatchCompute(_fluidSimVolumeCS, kernel, dispatchX, dispatchY, dispatchZ);
                }
            }
        }
        public void CopyTextureToAtlas(CommandBuffer cmd)
        {
            int kernel = _texture3DAtlasCS.FindKernel("CopyTexture");

            cmd.SetComputeTextureParam(_texture3DAtlasCS, kernel, HDShaderIDs._OutputVolumeAtlas, volumeAtlas);
            foreach (var volume in _volumes)
            {
                var initialSimTexture = volume.parameters.initialStateTexture;
                if (initialSimTexture == null)
                    continue;

                var inputVolumeTexture = volume.fSimTexture;
                if (inputVolumeTexture == null)
                    continue;

                int fluidSimVolumeResX = initialSimTexture.width;
                int fluidSimVolumeResY = initialSimTexture.height;
                int fluidSimVolumeResZ = initialSimTexture.depth;

                const int threadTile = 4;
                const int lessTile = threadTile - 1;

                int dispatchX = (fluidSimVolumeResX + lessTile) / threadTile;
                int dispatchY = (fluidSimVolumeResY + lessTile) / threadTile;
                int dispatchZ = (fluidSimVolumeResZ + lessTile) / threadTile;

                cmd.SetComputeTextureParam(_texture3DAtlasCS, kernel, HDShaderIDs._InputVolumeTexture, inputVolumeTexture);

                cmd.DispatchCompute(_texture3DAtlasCS, kernel, dispatchX, dispatchY, dispatchZ);
            }
        }

        public FluidSimVolume[] PrepareFluidSimVolumeData(CommandBuffer cmd, Camera currentCam, float time)
        {
            return _volumes.ToArray();
        }

        public void TriggerVolumeAtlasRefresh()
        {
            atlasNeedsRefresh = true;
        }
    }
}
