(function(P) {
  "use strict";
  P.createGPU=function(canvas) {
    canvas.width=960;canvas.height=600;
    const gl=canvas.getContext("webgl2",{alpha:false,antialias:false,preserveDrawingBuffer:true});
    if(!gl) throw Error("此浏览器未提供WebGL2，未伪造绘制结果。");
    function shader(type,text) {
      const s=gl.createShader(type);gl.shaderSource(s,text);gl.compileShader(s);
      if(!gl.getShaderParameter(s,gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(s));return s;
    }
    const vs=shader(gl.VERTEX_SHADER,`#version 300 es
      layout(location=0) in vec2 position;layout(location=1) in vec2 offset;
      uniform vec2 camera;uniform vec2 span;
      void main(){vec2 p=(position+offset-camera)*2.0/span;gl_Position=vec4(p.x,-p.y,0,1);}`);
    const fs=shader(gl.FRAGMENT_SHADER,`#version 300 es
      precision mediump float;uniform vec3 color;out vec4 result;
      void main(){result=vec4(color,1);}`);
    const program=gl.createProgram();gl.attachShader(program,vs);gl.attachShader(program,fs);gl.linkProgram(program);
    if(!gl.getProgramParameter(program,gl.LINK_STATUS)) throw Error(gl.getProgramInfoLog(program));
    gl.deleteShader(vs);gl.deleteShader(fs);
    const uniforms={camera:gl.getUniformLocation(program,"camera"),
      span:gl.getUniformLocation(program,"span"),color:gl.getUniformLocation(program,"color")};
    let palette={rock:[.6,.6,.6],air:[.95,.95,.95],edge:[.2,.2,.2]};
    const debug=gl.getExtension("WEBGL_debug_renderer_info");
    const renderer=debug?gl.getParameter(debug.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER);
    const identity={renderer,vendor:debug?gl.getParameter(debug.UNMASKED_VENDOR_WEBGL):gl.getParameter(gl.VENDOR),
      version:gl.getParameter(gl.VERSION),software:/swiftshader|llvmpipe|software/i.test(renderer),
      output:[960,600]};
    function buffer(data,owned) {
      const b=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,b);gl.bufferData(gl.ARRAY_BUFFER,data,gl.STATIC_DRAW);
      owned.push({buffer:b,bytes:data.byteLength});return b;
    }
    function upload(mesh,owned) {
      return {triangles:buffer(mesh.triangles,owned),lines:buffer(mesh.lines,owned),
        triangleCount:mesh.triangles.length/2,lineCount:mesh.lines.length/2,bounds:mesh.bounds};
    }
    function build(prepared) {
      const started=performance.now(),owned=[];
      const chunks=prepared.chunks.map(m=>upload(m,owned));
      gl.finish();const chunkUploadMs=performance.now()-started,instanceStarted=performance.now();
      let batches=chunks;
      if(prepared.g.method!=="contour") {
        const sources=new Map([...prepared.sources].map(([key,m])=>[key,upload(m,owned)])),groups=new Map();
        for(const item of prepared.instances) {
          const source=sources.get(item.key),b=source.bounds;
          const cx=(b.x0+b.x1)/2+item.x,cy=(b.y0+b.y1)/2+item.y;
          const key=item.key+":"+Math.floor(cx/64)+":"+Math.floor(cy/64);
          if(!groups.has(key)) groups.set(key,{source,positions:[],
            bounds:{x0:Infinity,y0:Infinity,x1:-Infinity,y1:-Infinity}});
          const group=groups.get(key);group.positions.push(item.x,item.y);
          for(const axis of ["x","y"]) {
            group.bounds[axis+"0"]=Math.min(group.bounds[axis+"0"],b[axis+"0"]+item[axis]);
            group.bounds[axis+"1"]=Math.max(group.bounds[axis+"1"],b[axis+"1"]+item[axis]);
          }
        }
        batches=[...groups.values()].map(g=>({...g.source,bounds:g.bounds,
          offsetBuffer:buffer(new Float32Array(g.positions),owned),instances:g.positions.length/2}));
      }
      gl.finish();
      return {chunks,batches,uploadMs:performance.now()-started,chunkUploadMs,
        instanceUploadMs:prepared.g.method==="contour"?0:performance.now()-instanceStarted,
        alias:prepared.g.method==="contour",
        uploadBytes:owned.reduce((s,b)=>s+b.bytes,0),
        dispose:()=>owned.forEach(b=>gl.deleteBuffer(b.buffer))};
    }
    function render(scene,policy,camera) {
      if(gl.isContextLost()) throw Error("WebGL上下文已丢失，停止绘制／计时；请重开后重测。");
      gl.viewport(0,0,960,600);gl.disable(gl.DEPTH_TEST);gl.disable(gl.BLEND);
      gl.clearColor(...palette.rock,1);gl.clear(gl.COLOR_BUFFER_BIT);gl.useProgram(program);
      gl.uniform2f(uniforms.camera,camera.x,camera.y);gl.uniform2f(uniforms.span,camera.width,camera.height);
      const visible=(policy==="instances"?scene.batches:scene.chunks).filter(m=>
        m.bounds.x1>=camera.x-camera.width/2&&m.bounds.x0<=camera.x+camera.width/2&&
        m.bounds.y1>=camera.y-camera.height/2&&m.bounds.y0<=camera.y+camera.height/2);
      let calls=0,vertices=0;
      for(const [field,count,primitive,color] of [
        ["triangles","triangleCount",gl.TRIANGLES,palette.air],["lines","lineCount",gl.LINES,palette.edge]]) {
        gl.uniform3fv(uniforms.color,color);
        for(const mesh of visible) {
          if(!mesh[count]) continue;
          gl.bindBuffer(gl.ARRAY_BUFFER,mesh[field]);gl.enableVertexAttribArray(0);
          gl.vertexAttribPointer(0,2,gl.FLOAT,false,0,0);gl.vertexAttribDivisor(0,0);
          if(mesh.offsetBuffer) {
            gl.bindBuffer(gl.ARRAY_BUFFER,mesh.offsetBuffer);gl.enableVertexAttribArray(1);
            gl.vertexAttribPointer(1,2,gl.FLOAT,false,0,0);gl.vertexAttribDivisor(1,1);
            gl.drawArraysInstanced(primitive,0,mesh[count],mesh.instances);vertices+=mesh[count]*mesh.instances;
          } else {
            gl.disableVertexAttribArray(1);gl.vertexAttrib2f(1,0,0);gl.vertexAttribDivisor(1,0);
            gl.drawArrays(primitive,0,mesh[count]);vertices+=mesh[count];
          }
          calls++;
        }
      }
      return {calls,vertices,batches:visible.length};
    }
    return {identity,build,render,finish:()=>gl.finish(),
      error:()=>gl.isContextLost()?gl.CONTEXT_LOST_WEBGL:gl.getError(),
      colors:p=>{palette=p;},read:()=>{
        const bytes=new Uint8Array(960*600*4);gl.readPixels(0,0,960,600,gl.RGBA,gl.UNSIGNED_BYTE,bytes);return bytes;
      },dispose:()=>gl.deleteProgram(program)};
  };
})(globalThis.CavePerf);
