(function(root){
  "use strict";
  const M=root.TerrainBench;
  class Renderer{
    constructor(canvas,art,mode){
      this.canvas=canvas;this.art=art;this.mode=mode;this.pages=new Map();this.bytes=0;this.uploaded=0;
      const gl=canvas.getContext("webgl",{alpha:false,antialias:false,preserveDrawingBuffer:true});
      if(!gl)throw Error("WebGL 不可用");this.gl=gl;
      const shader=(type,source)=>{const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;};
      this.program=gl.createProgram();
      gl.attachShader(this.program,shader(gl.VERTEX_SHADER,"attribute vec4 data;uniform vec4 view;varying vec2 uv;void main(){gl_Position=vec4((data.x-view.x)/view.z*2.-1.,1.-(data.y-view.y)/view.w*2.,0.,1.);uv=data.zw;}"));
      gl.attachShader(this.program,shader(gl.FRAGMENT_SHADER,"precision highp float;uniform sampler2D tex;uniform float snap;varying vec2 uv;void main(){vec2 p=uv;if(snap>0.)p=(floor(p*snap+.001)+.5)/snap;gl_FragColor=texture2D(tex,p);}"));
      gl.linkProgram(this.program);if(!gl.getProgramParameter(this.program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(this.program));
      this.attribute=gl.getAttribLocation(this.program,"data");this.view=gl.getUniformLocation(this.program,"view");
      this.snap=gl.getUniformLocation(this.program,"snap");
      this.shared=mode==="baked"?[]:(mode==="atlas"?[this.texture(art.atlas,false)]:[this.texture(art.rock,true),this.texture(art.floor,true),this.texture(art.ceiling,true)]);
      this.baseTextureBytes=mode==="baked"?0:this.shared.reduce((sum,t)=>sum+t.bytes,0);
      this.renderer=gl.getParameter(gl.RENDERER);
      this.scratch=new Uint8Array(4);gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);
    }
    texture(image,repeat){
      const gl=this.gl,t=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,t);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,repeat?gl.REPEAT:gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,repeat?gl.REPEAT:gl.CLAMP_TO_EDGE);
      gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,image);gl.bindTexture(gl.TEXTURE_2D,null);
      return{handle:t,bytes:image.width*image.height*4};
    }
    rebuild(a,keys){
      const gl=this.gl;let geometryMs=0,uploadMs=0,uploadBytes=0;
      for(const k of keys){
        let start=performance.now(),page=this.pages.get(k),arrays,source;
        if(this.mode==="baked"){
          source=TerrainArt.bake(a,k,this.art,page?.canvas);const b=M.bounds(k),q=[];
          M.quad(q,[[b.x,b.y],[b.x+32,b.y],[b.x+32,b.y+32],[b.x,b.y+32]],[[0,0],[1,0],[1,1],[0,1]]);
          arrays=[new Float32Array(q)];
        }else arrays=M.chunk(a,k,this.mode);
        geometryMs+=performance.now()-start;start=performance.now();
        if(!page){page={buffers:[],lengths:[],bytes:0,texture:null};this.pages.set(k,page);}
        this.bytes-=page.bytes;page.bytes=0;
        if(source){
          page.canvas=source;
          if(page.texture){gl.bindTexture(gl.TEXTURE_2D,page.texture.handle);gl.texSubImage2D(gl.TEXTURE_2D,0,0,0,gl.RGBA,gl.UNSIGNED_BYTE,source);}
          else page.texture=this.texture(source,false);
          uploadBytes+=512*512*4;
        }
        arrays.forEach((data,i)=>{
          if(!page.buffers[i])page.buffers[i]=gl.createBuffer();
          gl.bindBuffer(gl.ARRAY_BUFFER,page.buffers[i]);gl.bufferData(gl.ARRAY_BUFFER,data,gl.DYNAMIC_DRAW);
          page.lengths[i]=data.length/4;page.bytes+=data.byteLength;uploadBytes+=data.byteLength;
        });
        this.bytes+=page.bytes;uploadMs+=performance.now()-start;
      }
      this.uploaded+=uploadBytes;
      return{geometryMs,uploadMs,uploadBytes,chunks:keys.length,bufferBytes:this.bytes};
    }
    draw(view){
      const gl=this.gl;gl.viewport(0,0,this.canvas.width,this.canvas.height);gl.clearColor(.075,.105,.13,1);gl.clear(gl.COLOR_BUFFER_BIT);
      gl.useProgram(this.program);gl.uniform4f(this.view,view.x,view.y,view.w,view.h);gl.enableVertexAttribArray(this.attribute);
      let draws=0,vertices=0;
      for(const [k,page] of this.pages){
        const b=M.bounds(k);if(b.x>=view.x+view.w||b.y>=view.y+view.h||b.x+32<=view.x||b.y+32<=view.y)continue;
        page.buffers.forEach((buffer,i)=>{
          if(!page.lengths[i])return;
          gl.bindTexture(gl.TEXTURE_2D,(this.mode==="baked"?page.texture:this.shared[i]).handle);
          gl.uniform1f(this.snap,this.mode==="strip"&&i===0?64:0);
          gl.bindBuffer(gl.ARRAY_BUFFER,buffer);gl.vertexAttribPointer(this.attribute,4,gl.FLOAT,false,16,0);
          gl.drawArrays(gl.TRIANGLES,0,page.lengths[i]);draws++;vertices+=page.lengths[i];
        });
      }
      return{draws,vertices,textureBytes:this.baseTextureBytes+(this.mode==="baked"?this.pages.size*512*512*4:0),bufferBytes:this.bytes};
    }
    complete(){this.gl.readPixels(0,0,1,1,this.gl.RGBA,this.gl.UNSIGNED_BYTE,this.scratch);}
    dispose(){
      const gl=this.gl;
      for(const p of this.pages.values()){p.buffers.forEach(b=>gl.deleteBuffer(b));if(p.texture)gl.deleteTexture(p.texture.handle);}
      this.shared.forEach(t=>gl.deleteTexture(t.handle));gl.deleteProgram(this.program);gl.getExtension("WEBGL_lose_context")?.loseContext();
    }
  }
  root.TerrainRenderer=Renderer;
})(globalThis);
