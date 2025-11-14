(function(){
  var OrgChart = {
    orgData: [],
    filteredData: [],
    currentView: 'list',

    init: function(data){
      this.orgData = Array.isArray(data) ? data : [];
      this.filteredData = this.orgData.slice();
      this.initializeOrgChart();
      this.populateFilters();
      this.setupEventListeners();
    },

    qs: function(id){ return document.getElementById(id); },

    initializeOrgChart: function(){ this.renderListView(); },

    buildHierarchy: function(){
      var employeeMap = new Map();
      var roots = [];

      this.filteredData.forEach(function(emp) { employeeMap.set(emp.id, Object.assign({}, emp, { children: [] })); });

      this.filteredData.forEach(function(emp) {
        if (emp.manager === null || emp.manager === 0 || emp.manager === undefined || !employeeMap.has(emp.manager))
          roots.push(employeeMap.get(emp.id));
        else
          employeeMap.get(emp.manager).children.push(employeeMap.get(emp.id));
      });

      if (roots.length === 0) {
        var virtualRoot = { id: 0, name: 'Company', title: 'Top Level', department: 'All', status: 'Active', employeeNumber: '', email: '', profilePicture: null, children: [] };
        this.filteredData.forEach(function(emp) {
          if (emp.manager === null || emp.manager === 0 || emp.manager === undefined || !employeeMap.has(emp.manager))
            virtualRoot.children.push(employeeMap.get(emp.id));
        });
        return [virtualRoot];
      }
      return roots;
    },

    renderListView: function(){
      var container = this.qs('orgChartList');
      if (!container) return;

      var roots = this.buildHierarchy();
      container.innerHTML = '';
      var self = this;
      roots.forEach(function(root) { self.renderEmployeeList(root, container, 0); });
    },

    renderEmployeeList: function(employee, container, level){
      var row = document.createElement('div');
      row.className = 'org-employee level-' + level;
      row.style.marginLeft = (level * 30) + 'px';

      var card = document.createElement('div');
      card.className = 'card mb-2 shadow-sm' + (level === 0 ? ' border-primary' : '');
      var body = document.createElement('div'); body.className = 'card-body py-2';
      var flex = document.createElement('div'); flex.className = 'd-flex align-items-center';

      var hasChildren = employee.children && employee.children.length > 0;
      var left = document.createElement('div');
      if (hasChildren) {
        var toggleBtn = document.createElement('button');
        toggleBtn.className = 'btn btn-sm btn-outline-secondary me-2';
        toggleBtn.setAttribute('data-employee-id', employee.id);
        var icon = document.createElement('i'); icon.className = 'fas fa-chevron-down';
        toggleBtn.appendChild(icon);
        var self = this;
        toggleBtn.addEventListener('click', function(){ self.toggleChildren(this); });
        left.appendChild(toggleBtn);
      } else {
        left.style.width = '40px';
      }

      var avatarWrap = document.createElement('div'); avatarWrap.className = 'me-3';
      if (employee.profilePicture) {
        var img = document.createElement('img');
        img.src = employee.profilePicture; img.width = 50; img.height = 50; img.style.objectFit = 'cover'; img.style.borderRadius = '50%';
        avatarWrap.appendChild(img);
      } else {
        var ph = document.createElement('div');
        ph.className = 'bg-secondary rounded-circle d-inline-flex align-items-center justify-content-center text-white';
        ph.style.width = '50px'; ph.style.height = '50px'; ph.style.fontSize = '18px';
        ph.textContent = this.getInitials(employee.name);
        avatarWrap.appendChild(ph);
      }

      var middle = document.createElement('div'); middle.className = 'flex-grow-1';
      var midFlex = document.createElement('div'); midFlex.className = 'd-flex justify-content-between align-items-start';
      var info = document.createElement('div');
      var name = document.createElement('h6'); name.className = 'mb-1'; name.textContent = employee.name;
      var title = document.createElement('p'); title.className = 'mb-1 text-muted small'; title.textContent = employee.title || '';
      var dept = document.createElement('p'); dept.className = 'mb-0 small text-info'; dept.textContent = employee.department || '';
      info.appendChild(name); info.appendChild(title); info.appendChild(dept);

      var right = document.createElement('div'); right.className = 'text-end';
      var badge = document.createElement('span'); badge.className = 'badge ' + this.getStatusBadgeClass(employee.status); badge.textContent = employee.status; right.appendChild(badge);
      if (hasChildren) { var br = document.createElement('br'); var small = document.createElement('small'); small.className = 'text-muted'; small.textContent = ' ' + employee.children.length + ' reports'; right.appendChild(br); right.appendChild(small); }

      midFlex.appendChild(info); midFlex.appendChild(right); middle.appendChild(midFlex);

      var viewWrap = document.createElement('div'); viewWrap.className = 'ms-2';
      var viewBtn = document.createElement('button'); viewBtn.className = 'btn btn-sm btn-outline-primary';
      var eye = document.createElement('i'); eye.className = 'fas fa-eye'; viewBtn.appendChild(eye);
      var self = this;
      viewBtn.addEventListener('click', function(){ self.showEmployeeDetails(employee); });
      viewWrap.appendChild(viewBtn);

      flex.appendChild(left); flex.appendChild(avatarWrap); flex.appendChild(middle); flex.appendChild(viewWrap);
      body.appendChild(flex); card.appendChild(body); row.appendChild(card);
      container.appendChild(row);

      if (hasChildren) {
        var childrenContainer = document.createElement('div');
        childrenContainer.className = 'org-children';
        childrenContainer.id = 'children-' + employee.id;
        childrenContainer.style.display = 'block';
        var self = this;
        employee.children.forEach(function(c) { self.renderEmployeeList(c, childrenContainer, level + 1); });
        container.appendChild(childrenContainer);
      }
    },

    toggleChildren: function(button){
      var employeeId = button.getAttribute('data-employee-id');
      var childrenContainer = this.qs('children-' + employeeId);
      var icon = button.querySelector('i');
      if (!childrenContainer) return;
      if (childrenContainer.style.display === 'none') { childrenContainer.style.display = 'block'; icon.className = 'fas fa-chevron-down'; }
      else { childrenContainer.style.display = 'none'; icon.className = 'fas fa-chevron-right'; }
    },

    renderTreeView: function(){
      var container = this.qs('orgChartTree');
      if (!container) return;

      var roots = this.buildHierarchy();
      var metaRoot = { name: 'Company', children: roots };

      var nodeWidth = 180;
      var nodeHeight = 64;
      var xSpacing = 260;
      var ySpacing = 110;
      var nextY = 0;

      function layout(node, depth){
        node.depth = depth;
        if (!node.children || node.children.length === 0){
          node.y = nextY; nextY += 1;
        } else {
          node.children.forEach(function(ch) { layout(ch, depth + 1); });
          node.y = (node.children[0].y + node.children[node.children.length - 1].y) / 2;
        }
        node.x = depth;
      }

      layout(metaRoot, 0);

      var nodes = [];
      var links = [];
      (function collect(node){
        nodes.push(node);
        if (node.children){ node.children.forEach(function(ch) { links.push({s: node, d: ch}); collect(ch); }); }
      })(metaRoot);

      var maxDepth = Math.max.apply(Math, nodes.map(function(n) { return n.depth; }));
      var height = Math.max(600, (nextY) * ySpacing + 60);
      var width = Math.max(900, (maxDepth + 1) * xSpacing + nodeWidth + 120);

      container.innerHTML = '';
      var svg = document.createElementNS('http://www.w3.org/2000/svg','svg');
      svg.setAttribute('width', '100%');
      svg.setAttribute('height', String(height));
      svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
      svg.style.background = '#f8f9fa';

      var self = this;
      links.forEach(function(l){
        var x1 = l.s.x * xSpacing + nodeWidth/2 + 60;
        var y1 = l.s.y * ySpacing + nodeHeight/2 + 10;
        var x2 = l.d.x * xSpacing + nodeWidth/2 + 60;
        var y2 = l.d.y * ySpacing + nodeHeight/2 + 10;
        var path = document.createElementNS('http://www.w3.org/2000/svg','path');
        var d = 'M ' + x1 + ' ' + y1 + ' C ' + ((x1 + x2)/2) + ' ' + y1 + ', ' + ((x1 + x2)/2) + ' ' + y2 + ', ' + x2 + ' ' + y2;
        path.setAttribute('d', d);
        path.setAttribute('fill', 'none');
        path.setAttribute('stroke', '#c0c4c8');
        path.setAttribute('stroke-width', '2');
        svg.appendChild(path);
      });

      nodes.forEach(function(n){
        var cx = n.x * xSpacing + nodeWidth/2 + 60;
        var cy = n.y * ySpacing + nodeHeight/2 + 10;
        var g = document.createElementNS('http://www.w3.org/2000/svg','g');

        var rect = document.createElementNS('http://www.w3.org/2000/svg','rect');
        rect.setAttribute('x', String(cx - nodeWidth/2));
        rect.setAttribute('y', String(cy - nodeHeight/2));
        rect.setAttribute('rx', '8'); rect.setAttribute('ry', '8');
        rect.setAttribute('width', String(nodeWidth));
        rect.setAttribute('height', String(nodeHeight));
        rect.setAttribute('fill', '#ffffff');
        rect.setAttribute('stroke', '#adb5bd'); rect.setAttribute('stroke-width', '1.5');
        g.appendChild(rect);

        var dot = document.createElementNS('http://www.w3.org/2000/svg','circle');
        dot.setAttribute('cx', String(cx - nodeWidth/2 + 12));
        dot.setAttribute('cy', String(cy - nodeHeight/2 + 12));
        dot.setAttribute('r', '5');
        dot.setAttribute('fill', self.getStatusColor(n.status || 'Active'));
        dot.setAttribute('stroke', '#ffffff');
        dot.setAttribute('stroke-width', '2');
        g.appendChild(dot);

        var fo = document.createElementNS('http://www.w3.org/2000/svg','foreignObject');
        fo.setAttribute('x', String(cx - nodeWidth/2 + 22));
        fo.setAttribute('y', String(cy - nodeHeight/2 + 6));
        fo.setAttribute('width', String(nodeWidth - 32));
        fo.setAttribute('height', String(nodeHeight - 12));

        var div = document.createElement('div');
        div.setAttribute('xmlns', 'http://www.w3.org/1999/xhtml');
        div.style.fontFamily = 'system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif';
        div.style.lineHeight = '1.1';

        var safeName = (n.name || n.fullName || '').replace(/&/g,'&amp;').replace(/</g,'&lt;');
        var safeTitle = (n.title || '').replace(/&/g,'&amp;').replace(/</g,'&lt;');
        var isManager = (n.children && n.children.length > 0) || (n.directReports && n.directReports > 0);
        var iconClass = isManager ? 'fas fa-user-tie' : 'fas fa-user';
        var iconColor = isManager ? '#0d6efd' : '#6c757d';

        div.innerHTML = '<div style="display:flex;align-items:center;gap:6px;"><i class="' + iconClass + '" style="color:' + iconColor + ';font-size:12px"></i><div style="font-weight:700;font-size:12px;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;">' + safeName + '</div></div><div style="font-size:10px;color:#6c757d;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;margin-top:2px;">' + safeTitle + '</div>';
        fo.appendChild(div);
        g.appendChild(fo);

        g.style.cursor = 'pointer';
        g.addEventListener('click', function(){ if (n.id && n.id !== 0) self.showEmployeeDetails(n); });

        svg.appendChild(g);
      });

      container.appendChild(svg);
    },

    toggleView: function(){
      var listView = this.qs('listView');
      var treeView = this.qs('treeView');
      if (this.currentView === 'list') {
        this.currentView = 'tree';
        if (listView) listView.style.display = 'none';
        if (treeView) treeView.style.display = 'block';
        this.renderTreeView();
        var toggle = document.getElementById('viewToggle'); if (toggle) toggle.textContent = 'List View';
      } else {
        this.currentView = 'list';
        if (treeView) treeView.style.display = 'none';
        if (listView) listView.style.display = 'block';
        this.renderListView();
        var toggle = document.getElementById('viewToggle'); if (toggle) toggle.textContent = 'Tree View';
      }
    },

    showEmployeeDetails: function(employee){
      var modal = new bootstrap.Modal(this.qs('employeeModal'));
      var detailsDiv = this.qs('employeeDetails');
      var viewBtn = this.qs('viewEmployeeBtn');

      var wrapper = document.createElement('div');
      var row = document.createElement('div'); row.className = 'row'; wrapper.appendChild(row);

      var col1 = document.createElement('div'); col1.className = 'col-md-4 text-center';
      if (employee.profilePicture) {
        var img = document.createElement('img'); img.src = employee.profilePicture; img.className = 'img-fluid rounded-circle'; img.style.maxWidth = '100px'; col1.appendChild(img);
      } else {
        var ph = document.createElement('div'); ph.className = 'bg-secondary rounded-circle d-inline-flex align-items-center justify-content-center text-white'; ph.style.width = '100px'; ph.style.height = '100px'; ph.style.fontSize = '2rem'; ph.textContent = this.getInitials(employee.name || employee.fullName || ''); col1.appendChild(ph);
      }

      var col2 = document.createElement('div'); col2.className = 'col-md-8';
      var h5 = document.createElement('h5'); h5.textContent = employee.name || employee.fullName || ''; col2.appendChild(h5);
      var p2 = document.createElement('p'); p2.className = 'mb-1'; p2.innerHTML = '<strong>Title:</strong> ' + (employee.title || ''); col2.appendChild(p2);
      var p3 = document.createElement('p'); p3.className = 'mb-1'; p3.innerHTML = '<strong>Department:</strong> ' + (employee.department || ''); col2.appendChild(p3);
      var p4 = document.createElement('p'); p4.className = 'mb-1'; p4.innerHTML = '<strong>Email:</strong> <a href="mailto:' + (employee.email || '') + '">' + (employee.email || '') + '</a>'; col2.appendChild(p4);
      var p6 = document.createElement('p'); p6.className = 'mb-0'; p6.innerHTML = '<strong>Status:</strong> <span class="badge ' + this.getStatusBadgeClass(employee.status || 'Active') + '">' + (employee.status || 'Active') + '</span>'; col2.appendChild(p6);

      row.appendChild(col1); row.appendChild(col2);
      detailsDiv.innerHTML = ''; detailsDiv.appendChild(wrapper);
      if (employee.id && employee.id !== 0) viewBtn.href = '/Employees/Details/' + employee.id; else viewBtn.removeAttribute('href');
      modal.show();
    },

    getInitials: function(name){ return (name || '').split(' ').map(function(n){ return n[0]; }).join('').substring(0,2).toUpperCase(); },
    getStatusBadgeClass: function(status){ return status==='Active'?'bg-success':status==='OnLeave'?'bg-warning':status==='Inactive'?'bg-danger':'bg-secondary'; },
    getStatusColor: function(status){ return status==='Active'?'#28a745':status==='OnLeave'?'#ffc107':status==='Inactive'?'#dc3545':'#6c757d'; },

    populateFilters: function(){
      var departments = [];
      var seen = {};
      var self = this;
      this.orgData.forEach(function(d){ 
        if (d.department && !seen[d.department]) { 
          departments.push(d.department); 
          seen[d.department] = true; 
        } 
      });
      departments.sort();
      var deptSelect = this.qs('departmentFilter');
      if (deptSelect) {
        departments.forEach(function(dept){ 
          var opt = document.createElement('option'); 
          opt.value = dept; 
          opt.textContent = dept; 
          deptSelect.appendChild(opt); 
        });
      }
    },

    setupEventListeners: function(){
      var s = this.qs('searchEmployee'); if (s) s.addEventListener('input', this.filterChart.bind(this));
      var d = this.qs('departmentFilter'); if (d) d.addEventListener('change', this.filterChart.bind(this));
      var st = this.qs('statusFilter'); if (st) st.addEventListener('change', this.filterChart.bind(this));
    },

    filterChart: function(){
      var searchTerm = (this.qs('searchEmployee') && this.qs('searchEmployee').value || '').toLowerCase();
      var deptFilter = this.qs('departmentFilter') && this.qs('departmentFilter').value || '';
      var statusFilter = (this.qs('statusFilter') && this.qs('statusFilter').value || '');

      var self = this;
      this.filteredData = this.orgData.filter(function(emp){
        var matchesSearch = !searchTerm || (emp.name||'').toLowerCase().indexOf(searchTerm) >= 0 || (emp.title||'').toLowerCase().indexOf(searchTerm) >= 0;
        var matchesDept = !deptFilter || emp.department === deptFilter;
        var matchesStatus = !statusFilter || emp.status === statusFilter;
        return matchesSearch && matchesDept && matchesStatus;
      });

      if (!this.currentView) this.currentView = 'list';
      if (this.currentView === 'list') this.renderListView(); else this.renderTreeView();
    },

    clearFilters: function(){
      var s = this.qs('searchEmployee'); if (s) s.value = '';
      var d = this.qs('departmentFilter'); if (d) d.value = '';
      var st = this.qs('statusFilter'); if (st) st.value = '';
      this.filteredData = this.orgData.slice();
      if (this.currentView === 'list') this.renderListView(); else this.renderTreeView();
    },

    downloadOrgChart: function(format){
      var element = this.qs(this.currentView === 'list' ? 'orgChartList' : 'orgChartTree');
      if (format === 'png') {
        html2canvas(element, { backgroundColor: '#ffffff', scale: 2 }).then(function(canvas){
          var a = document.createElement('a'); a.download = 'organization-chart.png'; a.href = canvas.toDataURL(); a.click();
        });
      } else if (format === 'pdf') {
        var self = this;
        html2canvas(element).then(function(canvas){
          var jsPDF = window.jspdf.jsPDF; var pdf = new jsPDF('landscape', 'mm', 'a4');
          var imgData = canvas.toDataURL('image/png'); var imgWidth = 287; var pageHeight = 200; var imgHeight = (canvas.height * imgWidth) / canvas.width; var heightLeft = imgHeight; var position = 5;
          pdf.addImage(imgData, 'PNG', 5, position, imgWidth, imgHeight); heightLeft -= pageHeight;
          while (heightLeft >= 0) { position = heightLeft - imgHeight + 5; pdf.addPage(); pdf.addImage(imgData, 'PNG', 5, position, imgWidth, imgHeight); heightLeft -= pageHeight; }
          pdf.save('organization-chart.pdf');
        });
      }
    },

    printOrgChart: function(){
      var sourceEl = this.qs(this.currentView === 'list' ? 'orgChartList' : 'orgChartTree');
      var clone = sourceEl.cloneNode(true);
      var w = window.open('', '_blank');
      w.document.open();
      w.document.write('<!doctype html><html><head><meta charset="utf-8"><title>Organization Chart</title>');
      w.document.write('<link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css">');
      w.document.write('<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css">');
      w.document.write('<style>.btn{display:none!important}.fas{font-family:"Font Awesome 5 Free";font-weight:900}.org-employee{transition:none}.card{break-inside:avoid}</style>');
      w.document.write('</head><body><div class="container-fluid"><h2>Organization Chart</h2><div id="printHost"></div></div></body></html>');
      w.document.close();
      var host = w.document.getElementById('printHost'); host.appendChild(w.document.importNode(clone, true));
      setTimeout(function(){ w.focus(); w.print(); w.close(); }, 500);
    }
  };

  window.OrgChart = OrgChart;
})();
