(function(T) {
  "use strict";
  const vertexSource=`#version 300 es
  precision highp float;
  layout(location=0) in vec2 position;
  layout(location=1) in vec2 texcoord;
  layout(location=2) in float kind;
  layout(location=3) in float variant;
  layout(location=4) in vec2 origin;
  layout(location=5) in float mask;
  uniform vec4 camera;
  out vec2 world;out vec2 uv;flat out float tag;flat out float ports;
  flat out vec2 base;flat out float shape;
  void main(){world=position;uv=texcoord;tag=kind;ports=variant;base=origin;shape=mask;
    vec2 p=(position-camera.xy)/camera.zw;gl_Position=vec4(p.x*2.-1.,1.-p.y*2.,0.,1.);}`;
  const fragmentSource=`#version 300 es
  precision highp float;
  in vec2 world;in vec2 uv;flat in float tag;flat in float ports;
  flat in vec2 base;flat in float shape;
  uniform sampler2D atlas;uniform sampler2D strip;
  uniform vec3 rock;uniform vec3 rim;uniform vec3 ink;uniform vec3 ecology;
  uniform float details;
  out vec4 color;
  float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
  float bit(float a,float b){return mod(floor(a/b),2.);}
  void main(){
    vec2 local=world-base;
    vec2 st=(vec2(mod(shape,4.),floor(shape/4.))+clamp(local,vec2(.001),vec2(.999)))/4.;
    float type=tag,depth=uv.y;
    if(tag<-.5){vec4 texel=texture(atlas,st);if(texel.a<.5)discard;
      type=round(texel.r*3.);depth=texel.g;}
    else if(tag>.5){if(local.x<0.||local.x>1.||local.y<0.||local.y>1.||
      texture(atlas,st).a<.5)discard;}
    vec2 point=world*.72,cell=floor(point),chosen=cell;
    float first=99.,second=99.;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
      vec2 id=cell+vec2(float(x),float(y));
      vec2 center=id+vec2(.15)+vec2(hash(id),hash(id+73.))*.70;
      float d=dot(point-center,point-center);
      if(d<first){second=first;first=d;chosen=id;}else second=min(second,d);
    }
    float facet=hash(chosen),grain=hash(floor(world*27.));
    vec3 c=mix(rock,ink,.03+facet*.17)+(grain-.5)*.012;
    float crack=1.-smoothstep(.012,.055,second-first);
    c=mix(c,ink,crack*.67);
    if(type>.5){
      float u=tag<-.5?world.x*.8+world.y*.37:uv.x;
      float brush=texture(strip,vec2(fract(u),clamp(depth,0.,1.))).r;
      vec3 surface=mix(rim,rock,type==1.? .04:type==2.? .39:.63);
      c=mix(surface,rock,smoothstep(.18,1.,depth)*.78);
      c*=.85+brush*.22;
      c=mix(ink,c,smoothstep(0.,.13,depth));
    }
    vec2 q=local-.5;
    float line=step(abs(q.x),.022)*(bit(ports,1.)*step(q.y,0.)+bit(ports,4.)*step(0.,q.y))+
      step(abs(q.y),.022)*(bit(ports,8.)*step(q.x,0.)+bit(ports,2.)*step(0.,q.x));
    c=mix(c,ecology,clamp(line*details,0.,1.)*.75);
    color=vec4(c,1.);
  }`;
  function compile(gl,type,source) {
    const shader=gl.createShader(type);gl.shaderSource(shader,source);gl.compileShader(shader);
    if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(shader));
    return shader;
  }
  T.atlasPixels = function(size=48) {
    const width=size*4,data=new Uint8Array(width*width*4);
    for(let mask=0;mask<16;mask++)for(let y=0;y<size;y++)for(let x=0;x<size;x++) {
      const u=(x+.5)/size,v=(y+.5)/size;
      if(!T.inside(mask,u,v))continue;
      let distance=Infinity,normal=[0,0];
      for(const line of T.templates[mask].segments)for(let j=0;j<line.length-1;j++) {
        const a=line[j],b=line[j+1],dx=b[0]-a[0],dy=b[1]-a[1],length=Math.hypot(dx,dy);
        const t=Math.max(0,Math.min(1,((u-a[0])*dx+(v-a[1])*dy)/(length*length)));
        const d=Math.hypot(u-a[0]-dx*t,v-a[1]-dy*t);
        if(d<distance){distance=d;normal=[-dy/length,dx/length];}
      }
      const kind=distance<.18?(normal[1]>.3?1:normal[1]<-.3?2:3):0;
      const i=((Math.floor(mask/4)*size+y)*width+(mask%4)*size+x)*4;
      data[i]=kind*85;data[i+1]=kind?Math.round(Math.min(1,distance/.18)*255):0;data[i+3]=255;
    }
    return {data,width};
  };
  let sharedAtlas=null;
  T.gpu = function(canvas) {
    const gl=canvas.getContext("webgl2",{alpha:false,antialias:false,preserveDrawingBuffer:true});
    if(!gl)throw Error("当前环境未提供 WebGL 2；无法执行这份真实网格对比。");
    const program=gl.createProgram(),vs=compile(gl,gl.VERTEX_SHADER,vertexSource),
      fs=compile(gl,gl.FRAGMENT_SHADER,fragmentSource);
    gl.attachShader(program,vs);gl.attachShader(program,fs);gl.linkProgram(program);
    if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));
    gl.deleteShader(vs);gl.deleteShader(fs);gl.useProgram(program);
    const uniforms={};
    for(const name of ["camera","atlas","strip","rock","rim","ink","ecology","details"])
      uniforms[name]=gl.getUniformLocation(program,name);
    function texture(unit,width,height,data,format=gl.RGBA) {
      const tex=gl.createTexture();gl.activeTexture(gl.TEXTURE0+unit);gl.bindTexture(gl.TEXTURE_2D,tex);
      gl.texImage2D(gl.TEXTURE_2D,0,format===gl.RED?gl.R8:gl.RGBA,width,height,0,format,gl.UNSIGNED_BYTE,data);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.REPEAT);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
      return tex;
    }
    const assetStart=performance.now();sharedAtlas??=T.atlasPixels();
    const atlas=texture(0,sharedAtlas.width,sharedAtlas.width,sharedAtlas.data);
    const brush=new Uint8Array(128*32);
    for(let y=0;y<32;y++)for(let x=0;x<128;x++)
      brush[y*128+x]=Math.max(0,Math.min(255,150+Math.sin(x*.19+y*.24)*27+(T.hash(x>>2,y>>2)%43)));
    const strip=texture(1,128,32,brush,gl.RED),buffers=new Map();
    gl.uniform1i(uniforms.atlas,0);gl.uniform1i(uniforms.strip,1);
    const pixel=new Uint8Array(4),info=gl.getExtension("WEBGL_debug_renderer_info");
    const api={gl,canvas,buffers,assetMs:performance.now()-assetStart,textureBytes:sharedAtlas.data.length+brush.length,
      renderer:info?gl.getParameter(info.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER)};
    api.upload=function(adapter,keys=null) {
      if(gl.isContextLost())throw Error("WebGL 上下文丢失；本轮测量无效。");
      const t=performance.now();let bytes=0;
      for(const key of keys??adapter.meshes.keys()) {
        const mesh=adapter.meshes.get(key),id=adapter.method+":"+key;
        let entry=buffers.get(id);
        if(!entry){entry={buffer:gl.createBuffer(),count:0};buffers.set(id,entry);}
        gl.bindBuffer(gl.ARRAY_BUFFER,entry.buffer);gl.bufferData(gl.ARRAY_BUFFER,mesh.data,gl.DYNAMIC_DRAW);
        entry.count=mesh.vertices;bytes+=mesh.data.byteLength;
      }
      return {ms:performance.now()-t,bytes};
    };
    api.draw=function(adapter,camera,palette,details=true,sync=false) {
      if(gl.isContextLost())throw Error("WebGL 上下文丢失；本轮测量无效。");
      const t=performance.now();gl.useProgram(program);gl.viewport(0,0,canvas.width,canvas.height);
      gl.clearColor(...palette.background,1);gl.clear(gl.COLOR_BUFFER_BIT);
      gl.uniform4f(uniforms.camera,camera.x,camera.y,camera.w,camera.h);
      for(const name of ["rock","rim","ink","ecology"])gl.uniform3fv(uniforms[name],palette[name]);
      gl.uniform1f(uniforms.details,details?1:0);
      let draws=0,vertices=0;
      for(const [key,mesh] of adapter.meshes) {
        const r=mesh.rect;
        if(r.x>camera.x+camera.w||r.y>camera.y+camera.h||r.x+r.w<camera.x||r.y+r.h<camera.y)continue;
        const entry=buffers.get(adapter.method+":"+key);if(!entry?.count)continue;
        gl.bindBuffer(gl.ARRAY_BUFFER,entry.buffer);
        for(const [loc,size,offset] of [[0,2,0],[1,2,2],[2,1,4],[3,1,5],[4,2,6],[5,1,8]]) {
          gl.enableVertexAttribArray(loc);gl.vertexAttribPointer(loc,size,gl.FLOAT,false,T.stride*4,offset*4);
        }
        gl.drawArrays(gl.TRIANGLES,0,entry.count);draws++;vertices+=entry.count;
      }
      const submit=performance.now()-t;
      if(sync){gl.finish();gl.readPixels(0,0,1,1,gl.RGBA,gl.UNSIGNED_BYTE,pixel);}
      return {submit,sync:sync?performance.now()-t:0,draws,vertices};
    };
    api.clear=function() {for(const entry of buffers.values())gl.deleteBuffer(entry.buffer);buffers.clear();};
    api.dispose=function() {api.clear();gl.deleteTexture(atlas);gl.deleteTexture(strip);gl.deleteProgram(program);};
    return api;
  };
})(globalThis.TerrainFour);
